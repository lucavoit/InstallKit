using InstallKit;

return InstallerHost.Run(args, plan =>
{
    var service = plan.Msi(MsiBuilder.Create("Hello Service")
        .WithManufacturer("Contoso")
        .WithUpgradeCode("0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10") // never change this
        .WithDescription("Sample Windows service installed with InstallKit.")
        .WithMainExecutable("HelloService.exe")
        .Content(c => c.Payload("HelloService"))
        .Shell(s => s.StartMenuShortcut(false))                  // a service needs no shortcut
        .Appearance(a => a
            .Modern()                                            // native modern dialogs, nothing needed on the target
            .AccentColor("#2E7D32"))
        .Lifecycle(l => l.OnAfterInstall(Setup.WriteInstallMarker)) // runs on the target machine, elevated
        .Machine(m => m.Service("HelloService.exe", "HelloService", s => s
            .WithDisplayName("Hello Service")
            .WithDescription("Writes a heartbeat to the event log every minute."))));

    // A bundle is a single setup .exe that installs prerequisites first, then the MSI.
    plan.Bundle(BundleBuilder.Create("Hello Service")
        .WithManufacturer("Contoso")
        .WithUpgradeCode("5a2e7c41-9d83-4b6f-8e15-c3a09f4d2b77") // the bundle's own code
        .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48)) // skipped when already installed
        .Packages(k => k.Msi(service)));
});

static class Setup
{
    // Install-time code is a static method; the InstallKit generator turns it into a custom action.
    public static void WriteInstallMarker(InstallEvent e)
    {
        if (e.IsInstalling && e.InstallDir != null)
            System.IO.File.WriteAllText(System.IO.Path.Combine(e.InstallDir, "installed.txt"), System.DateTime.Now.ToString("O"));
    }
}
