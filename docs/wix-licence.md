# WiX version and licence

← [Engines](engines.md) · [Docs index](README.md) · [Project layout](project-layout.md) →

InstallKit uses whichever `wix` version is pinned in `.config/dotnet-tools.json`; the default is **6.0.2**, the version WixSharp recommends. WiX extensions are pinned to the same version automatically.

| WiX | Licence of the official binaries | Fee | Acceptance |
|---|---|---|---|
| 5.0.2 and older | MS-RL (open source) | None | Not needed |
| 6.0.x | MS-RL source + [Open Source Maintenance Fee](https://wixtoolset.org/osmf/) EULA | Owed by users who generate revenue with WiX | Not enforced by the build |
| 7.x | MS-RL source + OSMF EULA | Owed by revenue-generating users with ≥ US$10,000 annual gross revenue | Required: the build fails with `IK004` until you set `InstallKitAcceptWixEula` |

In every version the source code stays MS-RL. The fee applies only to the official binaries, and binaries you build from source yourself are not covered by it. This summarises the EULA files shipped in the `wix` NuGet packages; it is not legal advice.

## Changing the WiX version

Run this in the folder that contains `.config/`:

```bash
dotnet tool update wix --version 5.0.2 --allow-downgrade   # or 6.0.2 / 7.0.1
```

That is the only change needed: local builds and the pipeline templates both run `dotnet tool restore`. For WiX 7 or later, also accept its EULA in the installer project:

```xml
<PropertyGroup>
  <InstallKitAcceptWixEula>wix7</InstallKitAcceptWixEula>
</PropertyGroup>
```

Only WiX 6 and 7 are confirmed to work with WixSharp 2.14.2; test 5.0.2 before you rely on it.

---
← [Engines](engines.md) · [Docs index](README.md) · [Project layout](project-layout.md) →
