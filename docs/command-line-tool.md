# Command line tool

← [Editor checks](editor-checks.md) · [Docs index](README.md) · [Azure DevOps](azure-devops.md) →

`src/InstallKit.Tool` builds the `installkit` command (a .NET 8 program that also runs on newer runtimes):

```bash
dotnet run --project src/InstallKit.Tool -c Release -- new-guid 2       # two fresh GUIDs for UpgradeCodes
dotnet run --project src/InstallKit.Tool -c Release -- inspect MyApp.msi            # what the MSI installs
dotnet run --project src/InstallKit.Tool -c Release -- inspect MyApp.msi --json     # the same as JSON
dotnet run --project src/InstallKit.Tool -c Release -- diff MyApp-1.0.msi MyApp-1.1.msi
```

- **`inspect`:** product name, version, codes, features, files with sizes, services, registry values, environment variables, shortcuts, firewall rules and custom actions.
- **`diff`:** what a change does to the product: `+` added, `-` removed and `~` changed files, services, registry values and so on. The product code is not compared, because every build gets a new one. Handy in a pull request or to check that an upgrade does not drop something.
- **Windows only for MSI files:** reading an MSI uses the Windows Installer API. `new-guid` works everywhere.
- **Not published as a dotnet tool yet:** that needs a new package id on nuget.org first; until then run it from source as above.

---
← [Editor checks](editor-checks.md) · [Docs index](README.md) · [Azure DevOps](azure-devops.md) →
