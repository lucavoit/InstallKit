# Custom actions

← [How it works](how-it-works.md) · [Docs index](README.md) · [Registry](registry.md) →

Code that runs **on the target machine** during the install:

```csharp
MsiBuilder.Create("My App")
    .Lifecycle(l => l
        .OnAfterInstall(Setup.Configure, "MODE")           // elevated, after files are copied
        .OnBeforeInstall(Setup.CheckPrerequisites)         // not elevated, before anything changes
        .RunAfterInstall("MyApp.exe", "--init-db")         // run a program (elevated by default)
        .RunBeforeUninstall("MyApp.exe", "--unregister")); // real uninstall only, not upgrades

static class Setup
{
    public static void Configure(InstallEvent e)
    {
        if (e.IsInstalling) File.WriteAllText(e.InstallDir + "mode.txt", e["MODE"] ?? "client");
    }

    public static void CheckPrerequisites(InstallEvent e)
    {
        if (!Environment.Is64BitOperatingSystem) e.Cancel("64-bit Windows is required.");
    }
}
```

- **Static methods only:** pass a static method (`.Lifecycle(l => l.OnAfterInstall(Setup.Configure))`), which must be `internal` or `public`. Lambdas, local functions and instance methods are rejected at compile time (`IK160`–`IK162`).
- **Reading properties after install:** in `OnAfterInstall`, only the properties you list (plus `INSTALLDIR`) can be read.
- **How they run:** the InstallKit source generator, shipped with `InstallKit.Abstractions`, emits one `[CustomAction]` entry point per handler in your installer assembly. The engine registers each one as an ordinary custom action, and nothing is looked up by reflection at install time. The `WixSharp` engine embeds the entry points as managed code; `WixSharpCore` compiles them to native code.
- **Upgrade-safe:** handlers receive the engine-neutral `InstallEvent`, never WixSharp types, so they keep working when the engine changes.
- **Advanced cases:** use `ConfigureWixSharp` with WixSharp's own `ManagedAction` or `ElevatedManagedAction`.

## When the handlers run

The order inside one install, with the standard Windows Installer actions around it:

| Order | What happens | Handler |
|---|---|---|
| 1 | The setup starts, before searches run | `OnLoad` (not elevated) |
| 2 | Costing is done, feature choices are final | (InstallKit records the feature states) |
| 3 | `InstallFiles` copies the files | |
| 4 | **Right after the files are copied** | `OnAfterInstall` (deferred, elevated for per-machine installs) |
| 5 | `InstallServices` registers the services, `StartServices` starts them | |
| 6 | `InstallFinalize`: programs from `RunAfterInstall` run just before it, so after the services started | `RunAfterInstall` |

`OnBeforeInstall` runs before step 3 (not elevated, before anything changes). Because `OnAfterInstall` runs before the services are registered and started, it is the place to write a configuration file the service reads on its first start. A `RunBeforeUninstall` program runs before the files are removed, on a real uninstall only.

## What the handler can ask

`OnAfterInstall` also runs on repair, change, upgrade and uninstall, and on removing a feature. Ask the event instead of looking at the files on disk:

| Member | True when |
|---|---|
| `IsFreshInstall` | first install of the product (not an upgrade) |
| `IsInstalling` | an install, including the new version of a major upgrade |
| `IsUpgrading` | a major upgrade replaces an older version |
| `IsRepairing` | repair |
| `IsChange` | the installed product is changed (features added or removed) |
| `IsUninstalling` | the product is removed |
| `IsFeatureSelected("Docs")` | the feature is installed once this install is done (ticked, kept or added) |
| `WasFeatureSelected("Docs")` | the feature was installed before this install |
| `ProductVersion` | the version of the MSI being installed |

A handler that must undo what a removed feature set up compares the two: `WasFeatureSelected("Docs") && !IsFeatureSelected("Docs")`. The feature name is the one given to `Feature(...)`.

Two more members describe who runs the setup: `InstallingUserName` and `InstallingUserSid` (the account the setup runs as, from `LogonUser` and `UserSID`) and `IsInstalledBySystem` (the setup runs as `LocalSystem`, for example from SCCM or Intune, so there is no interactive user to configure). When a standard user is elevated with another administrator's credentials, the account is that administrator, not the person at the keyboard.

## Failures

- **A handler that throws or calls `e.Fail("message")` fails the install** and rolls it back, in every stage (`e.Cancel(reason)` does the same). The message is written to the MSI log and shown to the user: in the setup dialog, or as an error in the log of a silent install (the exit code is `1603`).
- **Advisory work:** pass `InstallFailureMode.Warn` to log the problem and carry on: `.OnAfterInstall(Setup.TuneCache, InstallFailureMode.Warn)`. Use it for optional configuration whose failure must not undo the install.
- **Where `e.Log(...)` goes:** into the MSI log, which Windows Installer only writes when asked: `msiexec /i MyApp.msi /l*v install.log`. Search the log for `InstallKit`.

---
← [How it works](how-it-works.md) · [Docs index](README.md) · [Registry](registry.md) →
