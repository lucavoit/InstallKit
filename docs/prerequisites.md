# Prerequisites

← [Bundles](bundles.md) · [Docs index](README.md) · [Signing](signing.md) →

Helpers for the runtimes most apps need. Each adds the right silent arguments and a detect condition, so the runtime is only installed when the machine does not have it yet:

```csharp
plan.Bundle(BundleBuilder.Create("My App")
    .WithUpgradeCode("5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77")
    .Prerequisites(p => p
        .VcRedist(@"prereqs\vc_redist.x64.exe")                                              // embedded in the setup
        .DotNetRuntime(DotNetRuntimeKind.WindowsDesktop, 8, @"prereqs\windowsdesktop-runtime-8.0.11-win-x64.exe")
        .WebView2(PrerequisiteInstaller.FromDownload(                                           // downloaded when the setup runs
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703", sha512: "<128 hex digits>", sizeInBytes: 1_800_000,
        fileName: "MicrosoftEdgeWebview2Setup.exe")))
    .Packages(k => k.Msi(app)));
```

- **Two ways to supply the installer:** a file you download once and keep in your repository (`p.VcRedist("path.exe")`, works offline), or `PrerequisiteInstaller.FromDownload(url, sha512, sizeInBytes)`, which keeps the setup small. The setup refuses a download whose hash or size differs, so use the values of exactly what the URL serves and update them when Microsoft updates the file. Compute them with `Get-FileHash -Algorithm SHA512 file.exe` and `(Get-Item file.exe).Length`.
- **Detection:** `VcRedist` looks for the Visual C++ 2015 to 2022 runtime of that architecture (`Installed` in the registry); `DotNetRuntime(kind, major, installer)` finds an installed runtime of that major version (`Runtime`, `AspNetCore` or `WindowsDesktop`); `WebView2` checks the machine and the user registry. Where it applies they take an `architecture` argument (x64 by default).
- **One per kind:** using the same helper twice for one runtime, version and architecture fails with `IK264`.
- **Checked before WiX runs:** an embedded file that is missing (`IK133`); a download that is not https (`IK260`), has no valid SHA-512 hash (`IK261`), no size up to 2 GB (`IK262`) or an unusable file name (`IK263`); a .NET major version below 3 (`IK265`).

---
← [Bundles](bundles.md) · [Docs index](README.md) · [Signing](signing.md) →
