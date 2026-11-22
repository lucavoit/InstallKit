using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InstallKit.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace InstallKit.Tests;

public class GeneratorTests
{
    // Minimal stand-in for WiX's DTF library, so the generated code can be compiled and checked here.
    const string DtfStub = """
        namespace WixToolset.Dtf.WindowsInstaller
        {
            public enum ActionResult { Success, Failure }
            public enum InstallRunMode { Scheduled }
            public enum InstallState { NotUsed = -7, Unknown = -1, Absent = 2, Local = 3 }
            public enum InstallMessage { Error = 0x01000000 }
            public sealed class Record : System.IDisposable
            {
                public Record(int fieldCount) { }
                public string FormatString { get; set; } = "";
                public string this[int field] { set { } }
                public void Dispose() { }
            }
            public sealed class FeatureInfo
            {
                public string Name { get; } = "";
                public InstallState CurrentState { get; }
                public InstallState RequestState { get; }
            }
            public sealed class CustomActionAttribute : System.Attribute { }
            public sealed class CustomActionData : System.Collections.Generic.Dictionary<string, string> { }
            public class Session
            {
                public string this[string name] { get => ""; set { } }
                public CustomActionData CustomActionData { get; } = new CustomActionData();
                public bool GetMode(InstallRunMode mode) => false;
                public void Log(string message) { }
                public System.Collections.Generic.IEnumerable<FeatureInfo> Features => new FeatureInfo[0];
                public void Message(InstallMessage kind, Record record) { }
            }
        }
        """;

    static (Compilation Output, IReadOnlyList<Diagnostic> Diagnostics) Run(string source, bool withDtf = true)
    {
        var sources = new List<string> { source };
        if (withDtf)
            sources.Add(DtfStub);

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Append(typeof(InstallEvent).Assembly.Location)
            .Append(Path.Combine(runtimeDir, "netstandard.dll"))
            .Append(Path.Combine(runtimeDir, "System.Runtime.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create("Installer",
            sources.Select(s => CSharpSyntaxTree.ParseText(s)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new InstallActionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        return (output, diagnostics);
    }

    [Fact]
    public void Static_handlers_get_compiling_entry_points()
    {
        var (output, diagnostics) = Run("""
            using InstallKit;
            namespace App
            {
                public static class Setup
                {
                    public static void Configure(InstallEvent e) { }
                    internal static class Nested { internal static void Check(InstallEvent e) { } }
                }
                static class Program
                {
                    static void Define() => MsiBuilder.Create("App")
                        .Lifecycle(l => l
                            .OnAfterInstall(Setup.Configure, "MODE")
                            .OnBeforeInstall(Setup.Nested.Check)
                            .OnLoad(Setup.Configure));
                }
            }
            """);

        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = string.Join("\n", output.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("\"App.Setup.Configure\"", generated);
        Assert.Contains("\"App.Setup+Nested.Check\"", generated);
        Assert.Contains(InstallActionGenerator.ActionName("AfterInstall", "App.Setup.Configure"), generated);
        Assert.Contains(InstallActionGenerator.ActionName("Load", "App.Setup.Configure"), generated);
    }

    [Theory]
    [InlineData("e => { }", "IK160")]
    [InlineData("new Holder().Instance", "IK161")]
    [InlineData("Holder.Hidden", "IK162")]
    public void Unsupported_handlers_are_reported(string argument, string code)
    {
        var (_, diagnostics) = Run($$"""
            using InstallKit;
            public class Holder
            {
                public void Instance(InstallEvent e) { }
                private static void Hidden(InstallEvent e) { }
                static void Define() => MsiBuilder.Create("App").Lifecycle(l => l.OnAfterInstall({{argument}}));
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == code);
    }

    [Fact]
    public void Nothing_is_generated_without_an_engine()
    {
        var (output, diagnostics) = Run("""
            using InstallKit;
            public static class Setup
            {
                public static void Configure(InstallEvent e) { }
                static void Define() => MsiBuilder.Create("App").Lifecycle(l => l.OnAfterInstall(Configure));
            }
            """, withDtf: false);

        Assert.Empty(diagnostics);
        Assert.Single(output.SyntaxTrees);
    }

    [Fact]
    public void Action_names_are_stable_and_msi_safe()
    {
        var name = InstallActionGenerator.ActionName("AfterInstall", "App.Setup.Configure");

        Assert.Equal(name, InstallActionGenerator.ActionName("AfterInstall", "App.Setup.Configure"));
        Assert.NotEqual(name, InstallActionGenerator.ActionName("Load", "App.Setup.Configure"));
        Assert.True(name.Length <= 72);
        Assert.All(name, c => Assert.True(char.IsLetterOrDigit(c) || c == '_'));
    }
}
