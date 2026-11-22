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

---
← [Optional features](optional-features.md) · [Docs index](README.md) · [Asking for values](prompted-values.md) →
