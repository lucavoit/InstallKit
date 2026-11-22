# InstallKit recipes

Complete examples. Replace the GUIDs with your own (`new-guid`).

## A Windows service with a locked data folder

```csharp
MsiBuilder.Create("My Service")
    .WithManufacturer("Contoso")
    .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10")
    .Content(c => c.Payload("MyService"))
    .Requirements(r => r.Requires64Bit())
    .Machine(m => m
        .Service("MyService.exe", "MyService", s => s.WithAccount(@"NT AUTHORITY\LocalService"))
        .DataFolder(Environment.SpecialFolder.CommonApplicationData, @"Contoso\My Service", f => f
            .Allow("SYSTEM", FolderRights.FullControl)
            .Allow("Administrators", FolderRights.FullControl)
            .Allow("NT AUTHORITY\\LocalService", FolderRights.Modify))
        .FirewallRule("My Service", "8080"))
    .Lifecycle(l => l.CloseApplication("MyService.exe", terminate: true));
```

`docs/folder-permissions.md`, `docs/firewall-rules.md`. Services are registered after `OnAfterInstall` handlers run, so a handler can write the config the service reads on its first start.

## Handlers that react to what is happening

```csharp
.Lifecycle(l => l.OnAfterInstall(Setup.Configure, "SERVER"))

static class Setup
{
    public static void Configure(InstallEvent e)
    {
        if (e.IsUninstalling) return;
        if (e.IsFreshInstall)
            File.WriteAllText(e.InstallDir + "server.txt", e["SERVER"] ?? "localhost");
        if (e.WasFeatureSelected("Docs") && !e.IsFeatureSelected("Docs"))
            e.Log("Docs were removed");
        if (e.IsInstalledBySystem)
            e.Log("Installed by a deployment tool: no interactive user");
    }
}
```

A handler that throws or calls `e.Fail("why")` rolls the install back and shows the message. Use `OnAfterInstall(Setup.Tune, InstallFailureMode.Warn)` for advisory work. Log goes to the MSI log (`msiexec /i x.msi /l*v log.txt`).

## Optional features

```csharp
.Feature("Documentation", "Help files", f => f.Directory(@"..\docs", "docs"))
.Feature("Samples", "Example projects", f => f.Directory(@"..\samples", "samples"), installedByDefault: false)
```

Command line: `msiexec /i MyApp.msi ADDLOCAL=Samples`. `docs/optional-features.md`.

## Ask for a server name

```csharp
.Settings(s => s.Prompt("SERVER", "Server address", defaultValue: "localhost", required: true))
.Machine(m => m.XmlValue("MyApp.exe.config", "/configuration/appSettings/add[@key='Server']/@value", "[SERVER]"))
```

Silent: `msiexec /i MyApp.msi /qn SERVER=prod.contoso.com`. Not for secrets. `docs/prompted-values.md`.

## Side-by-side versions

```csharp
.WithSideBySide(SideBySideMode.MajorMinor)   // 3.1.x and 3.5.x install next to each other
```

`docs/side-by-side.md`.

## Signing in a pipeline

Set `INSTALLKIT_SIGN_COMMAND` (or `InstallKitSignCommand`) to a command with `{file}`, e.g. `AzureSignTool sign ... {file}`. MSIs are signed before bundles embed them; bundle engines are re-signed. `docs/signing.md`, `docs/azure-devops.md` (`pipelines/stages/installer.yml`).

## Prerequisites in a bundle

```csharp
.Prerequisites(p => p
    .VcRedist(@"prereqs\vc_redist.x64.exe")
    .WebView2(PrerequisiteInstaller.FromDownload(url, sha512, sizeInBytes, "MicrosoftEdgeWebview2Setup.exe")))
.Packages(k => k.Msi(app))
```

`docs/prerequisites.md`.
