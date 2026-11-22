# Values in XML config files

← [Firewall rules](firewall-rules.md) · [Docs index](README.md) · [Closing running programs](closing-programs.md) →

```csharp
MsiBuilder.Create("My App")
    .Settings(s => s.Property("SERVER", "localhost"))
    .Machine(m => m.XmlValue("MyApp.exe.config", "/configuration/appSettings/add[@key='Server']/@value", "[SERVER]"));
```

Run `msiexec /i MyApp.msi SERVER=prod.contoso.com` and the installed file contains `prod.contoso.com`. This needs no custom action. To ask the user instead, see [Asking for values](prompted-values.md).

- **File:** relative to the install directory and part of a payload (`IK121`). The edit happens after the files are copied.
- **XPath:** points at one element or attribute and is checked before WiX runs (`IK221`). InstallKit escapes the `[` and `]` of XPath predicates for you, because Windows Installer would otherwise read them as property references.
- **Value:** MSI formatted text such as `[SERVER]` or `[INSTALLDIR]logs`.
- **XML only:** a JSON file fails with `IK222`. JSON support is planned.

---
← [Firewall rules](firewall-rules.md) · [Docs index](README.md) · [Closing running programs](closing-programs.md) →
