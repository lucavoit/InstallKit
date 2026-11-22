# Asking for values

← [Folder permissions](folder-permissions.md) · [Docs index](README.md) · [UI and branding](ui-and-branding.md) →

```csharp
MsiBuilder.Create("My App")
    .Settings(s => s
        .Prompt("SERVER", "Server address", defaultValue: "localhost", required: true)
        .Prompt("DB_NAME", "Database name", "myapp"))
    .Machine(m => m.XmlValue("MyApp.exe.config", "/configuration/appSettings/add[@key='Server']/@value", "[SERVER]"));
```

The default UI (and the other modern UIs) shows a settings page after the welcome page with a labelled input box for each value (at most three). The default fills the box, a **required** value keeps Next or Install disabled while the box is empty, and the page is followed by the feature page when the product has optional features.

- **Silent installs and the command line:** nothing is asked; `SERVER=prod` on the command line wins over the default, otherwise the default is used.
- **Where the value goes:** it is an ordinary MSI property, so `[SERVER]` works in XML values and run commands, and an install event reads it with `e["SERVER"]` (list it in `OnAfterInstall(handler, "SERVER")`).
- **Property names:** capital letters, digits and underscores (`IK280`); not one Windows Installer or InstallKit uses (`IK284`); not the same as a `Property` (`IK281`). The label is required (`IK282`) and only three prompts fit on the page (`IK283`).
- **Other UIs:** `Minimal`, `InstallDir`, `FeatureTree` and `ModernWpf` do not ask; the build prints warning `IK285` and the defaults apply. Use the WPF custom dialogs there.
- **Not for secrets:** the box shows what is typed and the value ends up in the install log. Use it for server names, regions, ports and the like.

---
← [Folder permissions](folder-permissions.md) · [Docs index](README.md) · [UI and branding](ui-and-branding.md) →
