---
name: installkit
description: Write, extend and debug Windows installers (MSI and setup.exe bundles) with InstallKit, a C# fluent API and MSBuild SDK on top of WixSharp and WiX. Use when a project has an InstallKit.Sdk installer project or `InstallerHost.Run`, or when someone wants an MSI, services, shortcuts, install-time C# handlers, prerequisites, signing or an Azure DevOps installer pipeline with C# and `dotnet build`. Also use to explain or fix IKxxx build messages.
---

# InstallKit

InstallKit describes a Windows installer as C# (`MsiBuilder`, `BundleBuilder`) in a small installer project that uses the `InstallKit.Sdk` MSBuild SDK. `dotnet build` publishes the payload projects, validates the plan (errors look like `InstallKit : error IK101: ...`), and builds the MSI and optional `setup.exe` bundle with WiX.

Full documentation is in the repository's `docs/` folder (start at `docs/README.md`). Read the page for the feature you touch before writing code; this skill is the map and the list of mistakes to avoid.

## Workflow

1. **Find the installer project:** a `.csproj` with `Sdk="InstallKit.Sdk/<version>"` and a `Program.cs` that calls `InstallerHost.Run(args, plan => ...)`. If there is none, create one: `dotnet new installkit -n MyApp.Installer --product "My App" --manufacturer Contoso --payload ../MyApp/MyApp.csproj`.
2. **Change the plan in `Program.cs`** with the fluent builders (see the cheat sheet). Do not hand-write WiX XML.
3. **Build:** `dotnet build <installer project> -p:Version=1.2.3 -p:InstallKitBuildOnBuild=true`. Building the MSI needs **Windows** and the pinned `wix` tool (`.config/dotnet-tools.json`, `dotnet tool restore`). On Linux or macOS the build only compiles the installer program; the MSI cannot be built, so say so instead of claiming it works.
4. **Read the messages:** each `IKxxx` code says what is wrong and how to fix it. `references/diagnostics.md` lists them all.
5. **Check the result** when a Windows machine is available: `dotnet run --project src/InstallKit.Tool -- inspect MyApp.msi` shows what the MSI installs, and `diff old.msi new.msi` shows what a change did.

## Skeleton

```csharp
using InstallKit;

return InstallerHost.Run(args, plan =>
{
    var app = plan.Msi(MsiBuilder.Create("My App")                    // always start from Create(...)
        .WithManufacturer("Contoso")
        .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10")      // a constant GUID, generated once, never changed
        .WithMainExecutable("MyApp.exe")                              // Start menu shortcut
        .Content(c => c.Payload("MyApp"))                             // published output of <InstallerPayload>
        .Shell(s => s.DesktopShortcut()));

    plan.Bundle(BundleBuilder.Create("My App")                        // optional: one setup.exe
        .WithUpgradeCode("5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77")      // a different GUID
        .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48))
        .Packages(k => k.Msi(app)));
});
```

```xml
<Project Sdk="InstallKit.Sdk/0.1.0-preview.1">
  <ItemGroup>
    <InstallerPayload Include="..\MyApp\MyApp.csproj" PublishProperties="RuntimeIdentifier=win-x64;SelfContained=true" />
  </ItemGroup>
</Project>
```

Without a payload project (files built elsewhere): leave out `InstallerPayload` and use `.Content(c => c.Directory(absoluteFolder))`.

## Cheat sheet

Scalar settings are generated `WithX(...)` methods (`WithManufacturer`, `WithUpgradeCode`, `WithMainExecutable`, `WithPlatform`, `WithSideBySide`, `WithInstallDirectory`, `WithVersion`, ...). Everything else is a group that takes a lambda:

| Group | Holds | Page |
|---|---|---|
| `Content(c => ...)` | `Payload`, `Directory` (always installed) | `docs/builders.md` |
| `Feature(name, description, f => ...)` | optional feature with its own files | `docs/optional-features.md` |
| `Appearance(a => ...)` | `Flat`, `Modern`, `Wpf`, `Classic`, `Theme`, `AccentColor`, `Logo` | `docs/ui-and-branding.md` |
| `Machine(m => ...)` | `Registry`, `EnvironmentVariable`, `PathEntry`, `FirewallRule`, `Service`, `FolderPermission`, `DataFolder`, `XmlValue` | `docs/registry.md`, `docs/environment-variables.md`, `docs/firewall-rules.md`, `docs/folder-permissions.md`, `docs/xml-values.md` |
| `Shell(s => ...)` | `FileAssociation`, `UrlProtocol`, `StartMenuShortcut`, `DesktopShortcut` | `docs/file-associations.md` |
| `Lifecycle(l => ...)` | `CloseApplication`, `OnLoad`, `OnBeforeInstall`, `OnAfterInstall`, `RunAfterInstall`, `RunBeforeUninstall` | `docs/custom-actions.md`, `docs/closing-programs.md` |
| `Requirements(r => ...)` | `Requires64Bit`, `RequiresWindows`, `LaunchCondition` | `docs/requirements.md` |
| `Settings(s => ...)` | `Property`, `Prompt` (values the dialogs ask for) | `docs/prompted-values.md` |

Bundles: `Prerequisites(p => ...)` (`NetFramework`, `VcRedist`, `DotNetRuntime`, `WebView2`), `Packages(k => ...)` (`Msi`, `Exe`), `Appearance(...)` (`docs/bundles.md`, `docs/prerequisites.md`).

## Rules that prevent the usual mistakes

- **The UpgradeCode is forever.** A constant GUID string, never `Guid.NewGuid()` and never changed (`IK300`, `IK301`). An MSI and a bundle each need their own (`IK103`, `IK302`). Generate one with `dotnet run --project src/InstallKit.Tool -- new-guid`.
- **Start every builder with `Create(...)`**, never `new MsiBuilder()` (`IK303`).
- **Install event handlers are static methods passed as method groups** (`l.OnAfterInstall(Setup.Configure)`), `internal` or `public`; no lambdas, local functions or instance methods (`IK160`-`IK162`). They run on the target machine, not during the build, and see `InstallEvent`, never WixSharp types. Details of the order, the phase flags (`IsFreshInstall`, `IsUpgrading`, `IsFeatureSelected`, ...) and failures (`e.Fail`, `InstallFailureMode.Warn`) are in `docs/custom-actions.md`.
- **Handlers and the flat default UI need .NET Framework 4.8 on the target** (the build adds a launch condition for it). The `WixSharpCore` engine compiles handlers to native code instead.
- **Versions:** MSI compares `major.minor.build` only (max `255.255.65535`); a prerelease suffix is dropped (`IK310`) so `1.2.3-rc1` equals `1.2.3` for Windows. Give each build its own number.
- **The package platform defaults to x64.** A payload published for another runtime gives warning `IK122`; use `WithPlatform(...)` (`docs/platform.md`).
- **Paths in the plan:** payload files and folders must exist at build time (`IK110`-`IK112`). Programs referenced by shortcuts, services, firewall rules and file associations must be in a payload (`IK121`).
- **`InstallKitBuildOnBuild` is off by default.** Pipelines pass `-p:InstallKitBuildOnBuild=true`.
- **WiX licence:** the default WiX 6 binaries come with the Open Source Maintenance Fee EULA; WiX 7 needs `InstallKitAcceptWixEula` (`IK004`). Do not accept a licence for the user; tell them (`docs/wix-licence.md`).
- **Raw WixSharp (`ConfigureWixSharp`) is the last resort**, because it ties the project to the WixSharp engine. Prefer a builder method or open an issue.

## Recipes

`references/recipes.md` has complete, copy-ready examples: a Windows service with a locked data folder, an upgrade-safe installer, side-by-side versions, optional features, signing in a pipeline, an installer that asks for a server name, and closing the running app before an upgrade.

## When something fails

| Symptom | Likely cause |
|---|---|
| `IK003` | MSI build requested off Windows; build on a Windows agent or drop `InstallKitBuildOnBuild=true` |
| `IK002` / wix not found | pin the tool: `dotnet new tool-manifest`, `dotnet tool install wix --version 6.0.2` |
| `IK111` | `Payload("Name")` has no matching `<InstallerPayload>`; the name is the project file name unless `Name=` is set |
| `IK311` | the build version does not fit MSI (`0.0.<huge counter>`); keep the counter in the 4th field |
| install runs but upgrade does not replace | UpgradeCode changed, or the version did not change in major.minor.build |

When a message is unclear, read the matching page in `docs/` and the exact check in `src/InstallKit.Abstractions/Validation.cs` before guessing.
