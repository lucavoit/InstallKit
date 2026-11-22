# InstallKit

[![NuGet](https://img.shields.io/nuget/vpre/InstallKit.Sdk?label=InstallKit.Sdk)](https://www.nuget.org/packages/InstallKit.Sdk)
[![NuGet downloads](https://img.shields.io/nuget/dt/InstallKit.Sdk)](https://www.nuget.org/packages/InstallKit.Sdk)
[![CI](https://github.com/lucavoit/InstallKit/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/lucavoit/InstallKit/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/lucavoit/InstallKit)](LICENSE)

Build Windows installers (MSI and setup `.exe` bundles) with plain C# and `dotnet build`. InstallKit sits on top of [WixSharp](https://github.com/oleg-shilo/wixsharp) and the [WiX Toolset](https://wixtoolset.org/), and adds a fluent API, an MSBuild SDK, a native modern installer UI, install-time C# handlers and ready-made Azure DevOps pipeline templates.

> **Version 1.0.** Feedback and issues are welcome.

## Why InstallKit

- **Installers as code:** describe the product, services, shortcuts and prerequisites in C#, with validation errors that point at the problem before WiX runs.
- **One command:** `dotnet build` (with `-p:InstallKitBuildOnBuild=true`) publishes your app, then produces the MSI and, optionally, a single `setup.exe` bundle.
- **Modern look by default:** a flat, dark installer UI in your colours (light, dark or following the Windows setting), with native dialogs that need nothing on the target machine as the alternative.
- **Install-time code in C#:** run static methods during install or uninstall, with no WixSharp types in your code.
- **CI included:** Azure DevOps templates that build, sign, smoke-test (install and uninstall) and publish.
- **Two engines:** WixSharp on .NET Framework 4.8, or a WixSharp.Core preview on .NET 10, with the same installer code.


## Requirements

- **Build machine:** Windows and the .NET SDK. WiX is restored as a pinned local tool.
- **Target machine:** usually nothing beyond Windows. See [What target machines need](docs/target-machines.md).

## Example

```csharp
using InstallKit;

return InstallerHost.Run(args, plan =>
{
    var app = plan.Msi(MsiBuilder.Create("My App")
        .WithManufacturer("Contoso")
        .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10")
        .WithMainExecutable("MyApp.exe")       // gets a Start menu shortcut
        .Content(c => c.Payload("MyApp"))      // published output of <InstallerPayload>
        .Shell(s => s.DesktopShortcut()));

    plan.Bundle(BundleBuilder.Create("My App") // optional: one setup.exe with prerequisites
        .WithUpgradeCode("5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77")
        .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48))
        .Packages(k => k.Msi(app)));
});
```

```xml
<Project Sdk="InstallKit.Sdk/1.0.0">
  <ItemGroup>
    <InstallerPayload Include="..\MyApp\MyApp.csproj" PublishProperties="RuntimeIdentifier=win-x64;SelfContained=true" />
  </ItemGroup>
</Project>
```

`dotnet build -p:Version=1.2.3 -p:InstallKitBuildOnBuild=true` publishes `MyApp`, then writes `bin/installer/Release/MyApp.msi` and `MyAppSetup.exe`.

Next: [Getting started](docs/getting-started.md).

## Documentation

Full documentation lives in [`docs/`](docs/README.md):

- **Start here:** [Getting started](docs/getting-started.md) · [Builders](docs/builders.md) · [Using your own build output](docs/own-build-output.md) · [How it works](docs/how-it-works.md)
- **Describe the installer:** [Custom actions](docs/custom-actions.md) · [Registry](docs/registry.md) · [Environment variables and PATH](docs/environment-variables.md) · [File associations and URL protocols](docs/file-associations.md) · [Firewall rules](docs/firewall-rules.md) · [Values in XML config files](docs/xml-values.md) · [Closing running programs](docs/closing-programs.md) · [Optional features](docs/optional-features.md) · [Folder permissions](docs/folder-permissions.md) · [Asking for values](docs/prompted-values.md) · [UI and branding](docs/ui-and-branding.md) · [Bundles](docs/bundles.md) · [Prerequisites](docs/prerequisites.md) · [Signing](docs/signing.md) · [Versions side by side](docs/side-by-side.md) · [Package platform](docs/platform.md) · [Requirements](docs/requirements.md)
- **Tooling:** [Editor checks](docs/editor-checks.md) · [Command line tool](docs/command-line-tool.md)
- **Ship it:** [Azure DevOps](docs/azure-devops.md) · [What target machines need](docs/target-machines.md) · [Engines](docs/engines.md) · [WiX version and licence](docs/wix-licence.md)
- **Project:** [Project layout](docs/project-layout.md) · [Contributing](docs/contributing.md) · [QA](docs/qa.md)

AI coding agents: see the [InstallKit skill](skills/installkit/SKILL.md).

## Roadmap

Planned work is tracked in the [open issues](https://github.com/lucavoit/InstallKit/issues) and their milestones.

## License

InstallKit is released under the [MIT License](LICENSE). WixSharp and the WiX Toolset have their own licences; see [WiX version and licence](docs/wix-licence.md).
