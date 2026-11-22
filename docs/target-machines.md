# What target machines need

← [Azure DevOps](azure-devops.md) · [Docs index](README.md) · [Engines](engines.md) →

The installer project only runs on your build machine. A target machine needs:

| Installer uses | Target machine needs |
|---|---|
| Plain MSI (classic UI, services, shortcuts, run commands) | Nothing beyond Windows: Windows Installer is built in |
| Default UI or `ModernFlat`, `WixSharp` engine | .NET Framework 4.8 (see the next row) |
| Native modern UI (`Modern`, `ModernInstallDir`, and the default with `WixSharpCore`) | Nothing |
| Install events (`OnAfterInstall`, ...), `WixSharp` engine | .NET Framework 4.8 (included in Windows 10 1903+, Windows 11 and Windows Server 2022+; install it on older systems) |
| Install events, `WixSharpCore` engine | Nothing: they are compiled to native code |
| `ModernWpf` UI | .NET Framework 4.8 |
| Bundle (setup `.exe`) | Nothing: the bundle engine and its standard UI are native code |
| Your application itself | Whatever it needs. Publish it self-contained (`SelfContained=true`, as in the sample) or chain its runtime into a bundle |

- **No InstallKit or WixSharp at install time:** neither is installed on the target machine. Custom-action code is embedded in the MSI and runs during installation only.

---
← [Azure DevOps](azure-devops.md) · [Docs index](README.md) · [Engines](engines.md) →
