# MyProduct.Installer

This project builds the installer of your product with [InstallKit](https://github.com/lucavoit/InstallKit). Describe the MSI (and bundle) in `Program.cs`.

## Build

```bash
# once per repository: pin the WiX toolset
dotnet new tool-manifest
dotnet tool install wix --version 6.0.2

# building the MSI is opt-in and needs Windows
dotnet build -p:Version=1.0.0 -p:InstallKitBuildOnBuild=true
```

A plain `dotnet build` only compiles the installer program. Set `<InstallKitBuildOnBuild>true</InstallKitBuildOnBuild>` in the project to build the MSI on every build.

## WiX licence (Open Source Maintenance Fee)

The official WiX 6.0.x binaries that this project pins by default are released under the [Open Source Maintenance Fee EULA](https://wixtoolset.org/osmf/): a fee is owed by users who generate revenue with WiX. WiX 7 and later need an explicit acceptance (`InstallKitAcceptWixEula`). WiX 5.0.2 and older are plain MS-RL; pin one of those with `dotnet tool update wix --version 5.0.2 --allow-downgrade` if the fee does not suit you (5.0.2 is only tested on a best-effort basis).

The build prints a note about this for WiX 6; hide it with `<InstallKitSuppressWixNotice>true</InstallKitSuppressWixNotice>` once you have read it.

More: https://github.com/lucavoit/InstallKit/tree/main/docs
