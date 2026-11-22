# Optional features

← [Closing running programs](closing-programs.md) · [Docs index](README.md) · [Folder permissions](folder-permissions.md) →

```csharp
MsiBuilder.Create("My App")
    .Content(c => c.Payload("MyApp"))                                          // always installed
    .Feature("Documentation", "Help files and the user guide", f => f
        .Directory(@"..\docs", "docs"))
    .Feature("Samples", "Example projects", f => f
        .Directory(@"..\samples", "samples"), installedByDefault: false);       // the default UI lets users tick them (or Classic(InstallerUi.FeatureTree))
```

- **A feature owns its files:** the lambda adds `Payload` and `Directory` entries to that feature, so a feature cannot be declared without its files, or the other way round.
- **Always-installed files:** everything added with `Content(...)`.
- **Command line:** `msiexec /i MyApp.msi ADDLOCAL=Samples` adds a feature and `REMOVE=Documentation` removes one. Spaces in a feature name become underscores (`Sample_files`).
- **Dialogs:** the default UI and the modern UIs (`ModernFlat`, `Modern`, `ModernInstallDir`) show a feature page after the welcome page (the flat UI starts the install from it; the native ones add a "ready to install" page); running the MSI again on an installed product offers Repair, Remove and Change, and Change opens the same feature page. `InstallerUi.FeatureTree` shows the classic tree. With another UI (`Minimal`, `InstallDir`, `ProgressOnly`, `ModernWpf`) the build prints warning `IK246` and the defaults apply.
- **Services and XML values can live in a feature:** the feature's files come with their service or XML edit, and nothing of it exists when the feature is left out. Add a service for an optional part with `.Feature("Worker", f => f.Payload("Worker", "worker")).Machine(m => m.Service(@"worker\Worker.exe", "MyWorker"))`.
- **Must always be installed:** the main executable (shortcuts point at it) and the programs used by file associations and firewall rules cannot be in a feature (`IK244`), because they are set up whether or not the feature is.
- **Checked before WiX runs:** an unusable name (`IK240`), duplicates (`IK241`), files for a feature that was never declared (`IK242`) and a feature with no files (`IK243`).

---
← [Closing running programs](closing-programs.md) · [Docs index](README.md) · [Folder permissions](folder-permissions.md) →
