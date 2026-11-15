using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InstallKit.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace InstallKit.Tests;

public class AnalyzerTests
{
    const string CodeA = "0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10";
    const string CodeB = "5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77";

    static async Task<string[]> Analyze(string body, bool referenceInstallKit = true)
    {
        var source = "using System; using InstallKit;\nclass Other { public Other WithUpgradeCode(string code) => this; }\nclass Installer { const string Fixed = \"" + CodeA + "\"; static readonly string Computed = Guid.NewGuid().ToString();\n void Run() {\n" + body + "\n} }";

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Append(Path.Combine(runtimeDir, "netstandard.dll"))
            .Append(Path.Combine(runtimeDir, "System.Runtime.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Where(path => referenceInstallKit || !path.EndsWith("InstallKit.Abstractions.dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create("Installer", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new InstallKitAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public async Task A_constant_guid_is_fine()
    {
        Assert.Empty(await Analyze($"MsiBuilder.Create(\"A\").WithUpgradeCode(\"{CodeB}\"); BundleBuilder.Create(\"B\").WithUpgradeCode(Fixed);"));
    }

    [Fact]
    public async Task A_new_guid_every_build_is_reported()
    {
        Assert.Equal(new[] { "IK300" }, await Analyze("MsiBuilder.Create(\"A\").WithUpgradeCode(Guid.NewGuid().ToString());"));
    }

    [Fact]
    public async Task A_value_that_is_not_a_constant_is_reported()
    {
        Assert.Equal(new[] { "IK300" }, await Analyze("MsiBuilder.Create(\"A\").WithUpgradeCode(Computed);"));
    }

    [Fact]
    public async Task The_func_overload_is_reported_because_it_runs_at_build_time()
    {
        Assert.Equal(new[] { "IK300" }, await Analyze($"MsiBuilder.Create(\"A\").WithUpgradeCode(() => \"{CodeA}\");"));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public async Task An_invalid_guid_is_reported(string value)
    {
        Assert.Equal(new[] { "IK301" }, await Analyze($"MsiBuilder.Create(\"A\").WithUpgradeCode(\"{value}\");"));
    }

    [Fact]
    public async Task Property_assignments_are_checked_too()
    {
        var body = $"var msi = MsiBuilder.Create(\"A\").Build(); msi.UpgradeCode = Guid.NewGuid().ToString(); var other = BundleBuilder.Create(\"B\").Build(); other.UpgradeCode = \"{CodeB}\";";

        Assert.Equal(new[] { "IK300" }, await Analyze(body));
    }

    [Fact]
    public async Task The_same_code_for_two_products_is_reported_at_both_places()
    {
        var body = $"MsiBuilder.Create(\"A\").WithUpgradeCode(\"{CodeA}\"); BundleBuilder.Create(\"B\").WithUpgradeCode(\"{CodeA.ToUpperInvariant()}\"); MsiBuilder.Create(\"C\").WithUpgradeCode(\"{CodeB}\");";

        Assert.Equal(new[] { "IK302", "IK302" }, await Analyze(body));
    }

    [Fact]
    public async Task A_builder_created_with_new_is_reported()
    {
        Assert.Equal(new[] { "IK303", "IK303", "IK303" }, await Analyze("new MsiBuilder(); new BundleBuilder(); new ServiceBuilder();"));
        Assert.Empty(await Analyze("MsiBuilder.Create(\"A\"); BundleBuilder.Create(\"B\"); ServiceBuilder.Create(\"S.exe\", \"S\");"));
    }

    [Fact]
    public async Task Unrelated_methods_with_the_same_name_are_ignored()
    {
        Assert.Empty(await Analyze("new Other().WithUpgradeCode(Guid.NewGuid().ToString());"));
    }

    [Fact]
    public async Task Projects_without_installkit_are_left_alone()
    {
        Assert.Empty(await Analyze("var x = 1;", referenceInstallKit: false));
    }
}
