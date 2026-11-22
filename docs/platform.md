# Package platform

← [Versions side by side](side-by-side.md) · [Docs index](README.md) · [Requirements](requirements.md) →

Every MSI is built for one platform. The default is **x64**.

```csharp
MsiBuilder.Create("My App")
    .WithPlatform(InstallerPlatform.X64)   // X64 (default), X86 or Arm64
```

What the platform decides:

- **Install folder:** an x64 or Arm64 package installs below `%ProgramFiles%` (`C:\Program Files`), an x86 package below `Program Files (x86)`.
- **Registry:** an x86 package writes to the 32-bit registry view (`WOW6432Node`); a 64-bit package writes to the 64-bit view. This applies to `Registry(...)` values and to the install-event handlers' own registry access.
- **Install events:** with an x86 package the handlers can run as 32-bit code, so open `RegistryView.Registry64` explicitly if they must see the 64-bit registry.

Files published for one runtime should go into a package of the same platform. The build compares the runtime identifier of every payload folder (from its `*.deps.json`, for example `win-x64`) with the package platform and prints warning `IK122` when they differ. Either set `WithPlatform(...)` to the platform of the files, or publish the payload for the platform of the package (`PublishProperties="RuntimeIdentifier=win-x64"`).

To check what a build produced, open the generated `.wxs` in `obj/installkit/`: the `Package` element carries the platform and the install directory starts at `ProgramFiles64Folder` or `ProgramFilesFolder`.

---
← [Versions side by side](side-by-side.md) · [Docs index](README.md) · [Requirements](requirements.md) →
