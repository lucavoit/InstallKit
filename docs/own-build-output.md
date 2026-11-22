# Using your own build output

← [Builders](builders.md) · [Docs index](README.md) · [How it works](how-it-works.md) →

You don't need a monorepo, and you don't need InstallKit to build your application. If your pipeline already produces the files to ship (a `dotnet publish` folder, a build artifact, files downloaded from another job), leave out `<InstallerPayload>` and give the installer the folder's path:

```csharp
using InstallKit;

// The pipeline that built the application passes the folder in.
var appDir = Environment.GetEnvironmentVariable("APP_PUBLISH_DIR")
    ?? throw new InvalidOperationException("Set APP_PUBLISH_DIR to the published application folder.");

return InstallerHost.Run(args, plan =>
{
    plan.Msi(MsiBuilder.Create("My App")
        .WithManufacturer("Contoso")
        .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10")
        .WithMainExecutable("MyApp.exe")       // relative to the folder; gets the Start menu shortcut
        .Content(c => c.Directory(appDir)));   // every file in the folder, keeping its structure
});
```

```xml
<Project Sdk="InstallKit.Sdk/0.1.0-preview.1">
  <!-- No InstallerPayload: the files come from outside this repository. -->
</Project>
```

```bash
APP_PUBLISH_DIR=/path/to/publish dotnet build MyApp.Installer -p:Version=1.2.3
```

- **Any path works:** absolute or relative, on any drive. A relative path is relative to the installer project folder, so prefer absolute paths from a pipeline.
- **Checked before WiX runs:** a folder that doesn't exist fails with `IK112`, and an MSI with no files at all fails with `IK110`.
- **Mix and match:** call `Directory` several times, optionally with a target subfolder (`.Content(c => c.Directory(docsDir, "docs"))`), and combine it with `Payload`.
- **Existing installers in a bundle:** `.Packages(k => k.Msi("path.msi"))` chains an MSI that was built elsewhere, and `k.Exe(path, ...)` chains any other setup program.
- **Your pipeline:** the build only needs a Windows agent, the .NET SDK and the pinned `wix` tool (`dotnet tool restore`). The Azure DevOps templates in `pipelines/` are optional.

---
← [Builders](builders.md) · [Docs index](README.md) · [How it works](how-it-works.md) →
