# Versions side by side

← [Signing](signing.md) · [Docs index](README.md) · [Package platform](platform.md) →

By default a newer version replaces the installed one (a major upgrade). To keep versions apart, so installing 3.5.6 does not remove 3.1.0, set a side-by-side mode:

```csharp
MsiBuilder.Create("My App")
    .WithManufacturer("Contoso")
    .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10")   // keep this forever; the per-line codes are derived from it
    .WithSideBySide(SideBySideMode.MajorMinor)
    .Machine(m => m.Service("MyService.exe", "MyService"));
```

| Mode | A new version replaces | Installs next to |
|---|---|---|
| `None` (default) | every older version | nothing |
| `Major` | versions with the same major version (3.1.0 and 3.5.6) | other majors (4.0.0) |
| `MajorMinor` | versions with the same major and minor version (3.1.0 and 3.1.9) | other minor versions (3.1.x and 3.5.x) |
| `Full` | nothing | every version |

What a line (for example `3.1`) changes at build time, so the products do not collide:

- **Name:** `My App 3.1`, which is the entry in Add or Remove Programs, the Start menu folder, the default install folder (`%ProgramFiles%\Contoso\My App 3.1`) and the default output file name (`MyApp3.1.msi`).
- **Upgrade code:** a stable GUID derived from yours and the line, so Windows Installer treats each line as its own product (and gives its components their own GUIDs). The code you write stays the same forever.
- **Services:** `MyService-3.1`, with the display name `MyService 3.1`, so two versions can run at once. Mind the ports and data folders they use.
- **Version line:** comes from the MSI's `Version`, or the `-p:Version` of the build.

Things to know:

- **A custom `InstallDirectory` must contain the line** (`IK270`), otherwise the versions share a folder.
- **Bundles:** a bundle that chains a side-by-side MSI needs the same `SideBySide` mode (`IK271`), otherwise a newer bundle replaces the older one together with its MSIs. A side-by-side bundle gets its name and upgrade code per line in the same way.
- **Machine-wide settings are not renamed:** registry values, environment variables, file associations, URL protocols and firewall rules keep the names you gave them, so versions share them. The build prints warning `IK273` when you use them together. Put the version into their names where they must not be shared.
- **Switching an existing product:** a build with a side-by-side mode has new upgrade codes, so it does not upgrade versions that were installed without one. Uninstall those first, or keep the mode `None` for the line you already shipped.

---
← [Signing](signing.md) · [Docs index](README.md) · [Package platform](platform.md) →
