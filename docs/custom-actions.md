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

---
← [How it works](how-it-works.md) · [Docs index](README.md) · [Registry](registry.md) →
