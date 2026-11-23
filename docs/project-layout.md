# Project layout

← [WiX version and licence](wix-licence.md) · [Docs index](README.md) · [Contributing](contributing.md) →

| Folder | What it is |
|---|---|
| `src/InstallKit.Abstractions` | Engine-neutral model: `MsiDefinition`, `BundleDefinition`, validation, `InstallerHost`. No WixSharp types. |
| `src/InstallKit.WixSharp` | The default engine: maps the model to WixSharp v2 on .NET Framework 4.8. |
| `src/InstallKit.WixSharp.Core` | Preview engine on WixSharp.Core (.NET 10). It compiles the same engine source. |
| `src/InstallKit.Sdk` | MSBuild SDK: publishes payloads, restores the pinned `wix` tool, runs the installer program. |
| `templates/` | `dotnet new installkit` project template (generates the upgrade codes for you). |
| `pipelines/` | Azure DevOps step, job and stage templates. |
| `samples/` | A Windows service with an MSI and a bundle, plus an example `azure-pipelines.yml`. |
| `tests/` | Unit tests for the model, version handling and validation. |
| `qa/` | End-to-end QA: a demo installer that uses most features, plus Pester tests that install, upgrade, repair and uninstall it on a Windows agent. |

---
← [WiX version and licence](wix-licence.md) · [Docs index](README.md) · [Contributing](contributing.md) →
