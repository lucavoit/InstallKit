# Editor checks

← [Versions side by side](side-by-side.md) · [Docs index](README.md) · [Command line tool](command-line-tool.md) →

`InstallKit.Abstractions` ships Roslyn analyzers that flag mistakes while you type, before the installer program runs:

| Code | Severity | Finds |
|---|---|---|
| `IK300` | Warning | An upgrade code that is not a constant string, such as `Guid.NewGuid().ToString()` or a `Func` overload. A different value on every build makes Windows treat each build as a new product, so upgrades stop working. |
| `IK301` | Error | An upgrade code that is not a valid GUID (or is all zeros). |
| `IK302` | Error | The same upgrade code used for two MSIs or bundles in one project. |
| `IK303` | Warning | `new MsiBuilder()`, `new BundleBuilder()` or `new ServiceBuilder()` instead of `Create(...)`, which would reset the model's defaults. |

They check `WithUpgradeCode(...)` and assignments to `UpgradeCode`. Everything else is still checked by the validators when the installer program runs, with the `IK1xx` codes above.

---
← [Versions side by side](side-by-side.md) · [Docs index](README.md) · [Command line tool](command-line-tool.md) →
