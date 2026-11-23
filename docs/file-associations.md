# File associations and URL protocols

← [Environment variables and PATH](environment-variables.md) · [Docs index](README.md) · [Firewall rules](firewall-rules.md) →

```csharp
MsiBuilder.Create("My App")
    .WithMainExecutable("MyApp.exe")
    .Shell(s => s
        .FileAssociation(".foo", "Foo document")                         // opens with MyApp.exe "%1"
        .FileAssociation(".bar", "Bar file", "tools/Viewer.exe")         // another program of the product
        .UrlProtocol("myapp", "My App link"));                           // myapp://... starts MyApp.exe "myapp://..."
```

- **Program:** defaults to `MainExecutable`; pass a path relative to the install directory to use another one. It must be in a payload (`IK121`), and one of the two must be given (`IK192`).
- **ProgId:** defaults to the output file name plus the extension (`MyApp.foo`); the fourth argument of `FileAssociation` sets it.
- **What it writes:** plain registry values under `HKEY_CLASSES_ROOT` (the extension's default value, the ProgId with an icon and an `open` command), so uninstall removes them like any other value.
- **Not forced on users:** Windows 10 and 11 keep the user's own "Open with" choice for an extension; the association only registers your program as a candidate and the default for extensions nobody else claimed.
- **Checked before WiX runs:** an invalid extension (`IK190`), scheme (`IK191`) or ProgId (`IK194`), and duplicates (`IK193`, `IK195`).

---
← [Environment variables and PATH](environment-variables.md) · [Docs index](README.md) · [Firewall rules](firewall-rules.md) →
