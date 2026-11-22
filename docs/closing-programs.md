# Closing running programs

← [Values in XML config files](xml-values.md) · [Docs index](README.md) · [Optional features](optional-features.md) →

```csharp
MsiBuilder.Create("My App")
    .WithMainExecutable("MyApp.exe")
    .Lifecycle(l => l
        .CloseApplication("MyApp.exe")                                      // asks it to close, then installs
        .CloseApplication("MyHelper.exe", terminate: true, timeoutSeconds: 10)); // ends it if it does not close
```

A running program locks its files, so an upgrade or uninstall would otherwise ask for a restart or leave files behind. Windows Installer asks the program to close first. With `terminate: true` it ends a program that is still running after the timeout; unsaved work in it is lost, so use it for helpers and services, not for programs people edit documents in.

- **Process name:** the executable's file name, such as `MyApp.exe`, without a path (`IK230`). It matches by name for every user on the machine.
- **When:** during the install, before files are replaced or removed (so for upgrades and uninstalls too).
- **Silent installs:** it works with `/qn` as well, which is the main use.
- **Checked before WiX runs:** a name that is not a file name (`IK230`), duplicates (`IK231`) and a timeout outside 1 to 600 seconds (`IK232`).

---
← [Values in XML config files](xml-values.md) · [Docs index](README.md) · [Optional features](optional-features.md) →
