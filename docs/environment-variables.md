# Environment variables and PATH

← [Registry](registry.md) · [Docs index](README.md) · [File associations and URL protocols](file-associations.md) →

```csharp
MsiBuilder.Create("My App")
    .Machine(m => m
        .PathEntry("[INSTALLDIR]bin")                      // adds the folder to PATH, removes only it on uninstall
        .PathEntry("[INSTALLDIR]tools", prepend: true)     // at the start of PATH instead
        .EnvironmentVariable("MYAPP_HOME", "[INSTALLDIR]"));
```

- **Per machine or per user:** a per-machine install changes the machine-wide variable, a per-user install the user's own.
- **Only your part is touched:** appended and prepended values are added to the existing value, and only that part is removed again.
- **Already running programs:** they keep the old environment. A new terminal or program sees the change.
- **Checked before WiX runs:** setting `PATH` as a whole fails with `IK182` (use `PathEntry`), a `;` inside one entry with `IK183`.

---
← [Registry](registry.md) · [Docs index](README.md) · [File associations and URL protocols](file-associations.md) →
