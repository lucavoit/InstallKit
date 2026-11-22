# Getting started

[Docs index](README.md) · [Builders](builders.md) →

The packages are on [nuget.org](https://www.nuget.org/packages?q=InstallKit). Use the same version for the SDK and the template:

```bash
dotnet new install InstallKit.Templates::1.0.0
dotnet new installkit -n MyApp.Installer --sdkVersion 1.0.0 --product "My App" --manufacturer Contoso --payload ../MyApp/MyApp.csproj --bundle
# add --engine WixSharpCore for the .NET 10 preview engine
# once per repository: pin the WiX toolset
dotnet new tool-manifest
# InstallKit needs the manifest at .config/dotnet-tools.json. The .NET 10 SDK creates dotnet-tools.json
# in the root instead, so if that happens, move it: create a .config folder and move the file into it.
dotnet tool install wix --version 6.0.2
dotnet build MyApp.Installer -p:Version=1.0.0 -p:InstallKitBuildOnBuild=true
```

Building the MSI requires **Windows** (WiX and the .NET Framework engine) and is opt-in: pass `-p:InstallKitBuildOnBuild=true` (or set the property in the project). Without it a normal `dotnet build` only compiles the installer program, so it also works on other OSes and in a solution build. If you opt in off Windows, the build stops with `IK003`.

## Requirements

- **Build machine:** Windows and the .NET SDK. WiX is restored as a pinned local tool.
- **Target machine:** usually nothing beyond Windows. See [What target machines need](target-machines.md).

---
[Docs index](README.md) · [Builders](builders.md) →
