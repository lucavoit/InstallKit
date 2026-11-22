# How it works

← [Using your own build output](own-build-output.md) · [Docs index](README.md) · [Custom actions](custom-actions.md) →

1. `dotnet build` compiles the installer project as a small .NET Framework console app.
2. The SDK runs `dotnet tool restore` so the `wix` version in `.config/dotnet-tools.json` is used. WiX extensions are pinned to that same version, so newer extensions on NuGet can't silently break the build.
3. Each `<InstallerPayload>` project is published into `obj/installkit/payload/<Name>/`, starting from an empty folder each time.
4. The SDK runs the program. `InstallerHost` validates the plan before WiX runs, then builds every MSI, then every bundle.
5. The built files end up in `@(InstallerOutput)`, and the generated `.wxs` is kept in `obj/installkit/` for review.

Validation errors use MSBuild's format (`error IK101: ...`), so they appear in Visual Studio's error list and in Azure DevOps annotations. Examples:
- A missing `UpgradeCode`. The error suggests a newly generated one.
- Upgrade codes shared between packages.
- An unknown payload name.
- A main executable that isn't in any payload.
- An `.exe` prerequisite without a `DetectCondition`.
- A version that doesn't fit MSI's `255.255.65535` limit.

## MSBuild properties

| Property | Default | |
|---|---|---|
| `Version` / `InstallKitVersion` | `1.0.0` | Prerelease and `+metadata` suffixes are dropped. |
| `InstallKitOutputDir` | `bin/installer/$(Configuration)/` | |
| `InstallKitBuildOnBuild` | `true` | Set to `false` for fast inner-loop builds. |
| `InstallKitRestoreTools` | `true` | Runs `dotnet tool restore`. |
| `InstallKitEmitSource` | `true` | Keeps the generated `.wxs`. |
| `InstallKitSignCommand` | empty | See [Signing](signing.md). |
| `InstallKitAcceptWixEula` | empty | `wix7` accepts the WiX 7 EULA. See [WiX version and licence](wix-licence.md). |
| `InstallKitEngine` | `WixSharp` | `WixSharp` or `WixSharpCore`. See [Engines](engines.md). |

To add your own steps, hook targets onto `InstallKitBuild`: `BeforeTargets="InstallKitBuild"` or `AfterTargets="InstallKitBuild"`. `@(InstallerOutput)` holds the built files.

## Escape hatch

Anything the model doesn't cover yet can be done with raw WixSharp:

```csharp
using InstallKit.WixSharp;

MsiBuilder.Create("My App")
    .ConfigureWixSharp(project => project.ControlPanelInfo.HelpLink = "https://contoso.com/support");
```

Code that uses this escape hatch is tied to the WixSharp engine, so move recurring needs into the model instead.

---
← [Using your own build output](own-build-output.md) · [Docs index](README.md) · [Custom actions](custom-actions.md) →
