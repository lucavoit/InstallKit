# Registry

← [Custom actions](custom-actions.md) · [Docs index](README.md) · [Environment variables and PATH](environment-variables.md) →

```csharp
MsiBuilder.Create("My App")
    .Machine(m => m
        .Registry(RegistryHive.LocalMachine, @"Software\Contoso\My App", "InstallPath", "[INSTALLDIR]")
        .Registry(RegistryHive.LocalMachine, @"Software\Contoso\My App", "Telemetry", 0)     // REG_DWORD
        .Registry(RegistryHive.CurrentUser, @"Software\Contoso\My App", "LogDir", @"%LOCALAPPDATA%\My App\logs", RegistryValueKind.ExpandString)
        .Registry(RegistryHive.LocalMachine, @"Software\Contoso\My App", null, "My App"));   // the key's default value
```

- **Formatted text:** string values can use MSI properties such as `[INSTALLDIR]`.
- **Uninstall:** Windows Installer removes the values with the files, and deletes a key it created once it is empty.
- **Checked before WiX runs:** a key with a leading backslash fails with `IK170`, a DWord that is not a whole number with `IK171`, and an HKLM value in a per-user install with `IK172`.
- **Machine-wide or per user:** use `RegistryHive.LocalMachine` for per-machine installs and `RegistryHive.CurrentUser` for per-user installs.

---
← [Custom actions](custom-actions.md) · [Docs index](README.md) · [Environment variables and PATH](environment-variables.md) →
