using InstallKit;

// The QA demo installer: uses as many InstallKit features as possible, each with an effect that
// qa/tests/Installer.Tests.ps1 asserts on a real Windows machine. Keep the two in sync.
return InstallerHost.Run(args, plan =>
{
    var msi = plan.Msi(MsiBuilder.Create("InstallKit QA Demo")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("7c3f0d52-8a41-4e6b-b1d9-2f54a8c3e917") // never change: the upgrade tests rely on it
        .WithDescription("QA demo installer for InstallKit.")
        .WithOutputFileName("QaDemo")
        .WithPayload("QaDemo")
        .WithDirectory(@"..\content\config", "config")
        .WithMainExecutable("QaDemo.exe")
        .WithStartMenuShortcut()
        .WithDesktopShortcut()
        .WithModernUi(chooseInstallDir: true)
        .WithAccentColor("#6A1B9A")
        .WithProperty("MODE", "client")                          // overridden on the msiexec command line
        .OnAfterInstall(QaSetup.WriteMode, "MODE")
        .WithRegistryValue(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "InstallPath", "[INSTALLDIR]")
        .WithRegistryValue(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", null, "InstallKit QA Demo")
        .WithRegistryValue(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "Telemetry", 0)
        .WithRegistryValue(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "LogDir", @"%ProgramData%\InstallKitQa", RegistryValueKind.ExpandString)
        .WithPathEntry("[INSTALLDIR]bin")
        .WithEnvironmentVariable("INSTALLKIT_QA_HOME", "[INSTALLDIR]")
        .WithFileAssociation(".ikqa", "InstallKit QA file")
        .WithUrlProtocol("installkitqa", "InstallKit QA link")
        .WithFirewallRule("InstallKit QA Demo port", "48765")
        .WithFirewallRule("InstallKit QA Demo program", executable: "QaDemo.exe", scope: FirewallScope.LocalSubnet, profile: FirewallProfile.Private)
        .RunAfterInstall("QaDemo.exe", "--init [MODE]")
        .RunBeforeUninstall("QaDemo.exe", "--unregister")
        .WithService("QaDemo.exe", "InstallKitQaDemo", s => s
            .WithDisplayName("InstallKit QA Demo")
            .WithDescription("Service installed by the InstallKit QA pipeline.")));

    plan.Bundle(BundleBuilder.Create("InstallKit QA Demo")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("e1a64b07-53c9-4f2d-9e8a-6b0d17c4f2a3")
        .WithOutputFileName("QaDemoSetup")
        .WithNetFramework(NetFrameworkVersion.NetFx48)
        .WithMsi(msi));
});

static class QaSetup
{
    // Install event: proves a C# handler ran elevated after the files were copied and could read MODE.
    public static void WriteMode(InstallEvent e)
    {
        if (!e.IsInstalling || e.InstallDir == null)
            return;

        e.Log("QaSetup.WriteMode running");
        System.IO.File.WriteAllText(System.IO.Path.Combine(e.InstallDir, "mode.txt"), e["MODE"] ?? "<missing>");
    }
}
