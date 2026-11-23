# Folder permissions

← [Optional features](optional-features.md) · [Docs index](README.md) · [Asking for values](prompted-values.md) →

```csharp
MsiBuilder.Create("My App")
    .Machine(m => m
        .Service("MyService.exe", "MyService", s => s.WithAccount(@"NT AUTHORITY\LocalService"))
        .FolderPermission(@"NT AUTHORITY\LocalService", FolderRights.Modify, "data")   // creates data\ if no payload has it
        .FolderPermission("Users", FolderRights.Read, "config")                         // a payload folder
        .FolderPermission("Authenticated Users", FolderRights.Modify));                 // the install folder itself
```

- **Accounts:** a name such as `Users`, `Everyone`, `Administrators` or `Authenticated Users`, or `DOMAIN\name` such as `NT AUTHORITY\LocalService` or `NT SERVICE\MyService`.
- **Rights:** `Read` (list, read, run), `Modify` (Windows' "Modify": also create, change and delete) and `FullControl`.
- **Folders:** the install directory (no folder given), a folder of a payload (`Directory(dir, "config")`), or any other relative path, which is created as an empty folder. Only that one folder is changed: never Program Files or the manufacturer folder above the install directory.
- **Not supported yet:** a folder inside a payload folder (`IK252`), such as `config\secrets` when `config` holds files.
- **Checked before WiX runs:** a missing account (`IK250`), a folder that is not below the install directory (`IK251`), and the same account twice for one folder (`IK253`).

## Folders outside the install directory (`DataFolder`)

A machine-wide data or configuration folder, for example below `%ProgramData%`, can be created by the MSI itself with the access you choose, before any install event handler or service runs. That is more reliable than creating and locking the folder from a handler or the service's first start.

```csharp
MsiBuilder.Create("My App")
    .Machine(m => m
        .DataFolder(Environment.SpecialFolder.CommonApplicationData, @"Contoso\My App", f => f
            .Allow("SYSTEM", FolderRights.FullControl)
            .Allow("Administrators", FolderRights.FullControl)
            .Allow("Users", FolderRights.Read)));
```

- **Root:** only `Environment.SpecialFolder.CommonApplicationData` (`%ProgramData%`) is supported (`IK330`); the path is relative, without `..`, a drive, wildcards or MSI properties (`IK331`).
- **Access:** at least one `Allow` is needed (`IK332`); the account names work as for `FolderPermission`. The same account twice fails with `IK334`, and the same folder twice with `IK335`.
- **What it writes:** a directory below the `CommonAppDataFolder` standard directory with a `PermissionEx` entry per account.
- **Not yet supported:** deny rules, switching off inheritance from the parent folder, an explicit owner, and removing the folder on uninstall. The MSI cannot express these with WiX's permission element alone; they need a custom action and are tracked in the issues #36 and #46. Until then the folder is kept on uninstall, and accounts that are not listed keep what the parent folder gives them.

---
← [Optional features](optional-features.md) · [Docs index](README.md) · [Asking for values](prompted-values.md) →
