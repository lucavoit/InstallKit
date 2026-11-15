using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using InstallKit;
using InstallKit.WixSharp;
using WixSharp;
using WixSharp.Bootstrapper;
using IOFile = System.IO.File;

namespace InstallKit.WixSharp.Engine.Tests;

/// <summary>Runs a model through the engine and gives back the WiX source it would hand to WiX.</summary>
static class Wxs
{
    public const string Code = "0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10";
    public const string BundleCode = "5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77";

    /// <summary>A temporary folder with an App.exe, a Svc.exe, app.config, config\a.json and docs\readme.txt.</summary>
    public sealed class Workspace
    {
        public Workspace()
        {
            Root = Directory.CreateTempSubdirectory().FullName;
            Payload = Path.Combine(Root, "payload");
            Directory.CreateDirectory(Path.Combine(Payload, "bin"));
            IOFile.WriteAllText(Path.Combine(Payload, "App.exe"), "x");
            IOFile.WriteAllText(Path.Combine(Payload, "Svc.exe"), "x");
            IOFile.WriteAllText(Path.Combine(Payload, "app.config"), "<configuration/>");

            Docs = Path.Combine(Root, "docs");
            Directory.CreateDirectory(Docs);
            IOFile.WriteAllText(Path.Combine(Docs, "readme.txt"), "x");

            Context = InstallerContext.FromEnvironment(new[]
            {
                "--version", "3.1.0",
                "--payload", $"App={Payload}",
                "--output", Path.Combine(Root, "out"),
                "--intermediate", Path.Combine(Root, "obj"),
            });
        }

        public string Root { get; }

        public string Payload { get; }

        public string Docs { get; }

        public InstallerContext Context { get; }

        public MsiBuilder App() =>
            MsiBuilder.Create("My App").WithManufacturer("Contoso").WithUpgradeCode(Code).WithPayload("App");
    }

    /// <summary>The WiX source of an MSI. Classic UI without branding, so no images are generated.</summary>
    public static XDocument Msi(Workspace workspace, MsiBuilder builder)
    {
        // The same steps as a build, in the same order: plan, side by side naming, validation, then the engine.
        var plan = new InstallerPlan(workspace.Context);
        var msi = plan.Msi(builder);
        SideBySide.Apply(plan);
        var errors = InstallerValidator.Validate(plan);
        if (errors.Count > 0)
            throw new InstallerValidationException(errors);

        var project = WixSharpEngine.CreateMsiProject(msi, workspace.Context);
        return Save(workspace, project.BuildWxs(Compiler.OutputType.MSI, Path.Combine(workspace.Root, "msi.wxs")));
    }

    public static XDocument Bundle(Workspace workspace, BundleBuilder builder, params MsiDefinition[] msis)
    {
        var bundle = builder.Build();
        var built = msis.ToDictionary(m => m, m => Path.Combine(workspace.Payload, "App.exe"));
        var project = WixSharpEngine.CreateBundleProject(bundle, built, workspace.Context);
        // WixSharp writes a bundle's source (setup.wxs) into the current directory.
        var previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Root);
        try
        {
            return Save(workspace, Compiler.BuildWxs(project));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    static XDocument Save(Workspace workspace, string path) => XDocument.Load(path);

    public static IEnumerable<XElement> All(this XDocument document, string name) => document.Descendants().Where(e => e.Name.LocalName == name);

    public static XElement One(this XDocument document, string name, string? id = null) =>
        document.All(name).Single(e => id == null || (string?)e.Attribute("Id") == id);
}
