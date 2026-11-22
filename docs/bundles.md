# Bundles

← [UI and branding](ui-and-branding.md) · [Docs index](README.md) · [Prerequisites](prerequisites.md) →

A bundle is a single setup `.exe` that installs its packages in chain order:

```csharp
plan.Bundle(BundleBuilder.Create("My App")
    .WithUpgradeCode("5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77")
    .Appearance(a => a.Logo("logo.png"))   // also Theme, AccentColor, BackgroundColor, TextColor
    .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48))
    .Packages(k => k
        .Exe(@"prereqs\vc_redist.x64.exe", e => e.DetectCondition = "VCRedistInstalled") // a Burn variable you set with a search
        .Msi(app)));
```

- `Packages(k => k.Msi(app))` is an MSI from the same plan. It is always built before the bundle.
- `k.Msi("path.msi")` is an existing MSI file.
- `Prerequisites(p => p.NetFramework(...))` installs .NET Framework from the web when it is missing.
- `k.Exe(path, e => e.DetectCondition = ...)` runs any other installer, such as the .NET runtime or VC++ redistributables. A detect condition is required so it is skipped when already installed.

The bundle has its own `UpgradeCode`, separate from its MSIs' codes.

---
← [UI and branding](ui-and-branding.md) · [Docs index](README.md) · [Prerequisites](prerequisites.md) →
