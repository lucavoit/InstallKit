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
        .WithMainExecutable("QaDemo.exe")
        .Content(c => c
            .Payload("QaDemo")
            .Directory(@"..\content\config", "config"))
        .Feature("Docs", "Documentation", f => f.Directory(@"..\content\docs", "docs"))
        .Feature("Samples", "Sample files, not installed unless asked for", f => f.Directory(@"..\content\samples", "samples"), installedByDefault: false)
        .Appearance(a => a
            .Modern(chooseInstallDir: true)
            .AccentColor("#6A1B9A"))
        .Shell(s => s
            .StartMenuShortcut()
            .DesktopShortcut()
            .FileAssociation(".ikqa", "InstallKit QA file")
            .UrlProtocol("installkitqa", "InstallKit QA link"))
        .Settings(s => s
            .Property("MODE", "client")                          // overridden on the msiexec command line
            // Asked for on the settings page of the modern UI; here it is the default or the command line that provides it.
            .Prompt("QA_REGION", "Region", "eu-west", required: true))
        .Machine(m => m
            .Registry(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "InstallPath", "[INSTALLDIR]")
            .Registry(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", null, "InstallKit QA Demo")
            .Registry(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "Telemetry", 0)
            .Registry(RegistryHive.LocalMachine, @"Software\InstallKit\QA Demo", "LogDir", @"%ProgramData%\InstallKitQa", RegistryValueKind.ExpandString)
            .PathEntry("[INSTALLDIR]bin")
            .EnvironmentVariable("INSTALLKIT_QA_HOME", "[INSTALLDIR]")
            .FirewallRule("InstallKit QA Demo port", "48765")
            .FirewallRule("InstallKit QA Demo program", executable: "QaDemo.exe", scope: FirewallScope.LocalSubnet, profile: FirewallProfile.Private)
            .XmlValue(@"config\qa.config", "/configuration/appSettings/add[@key='Server']/@value", "[MODE]")
            .XmlValue(@"config\qa.config", "/configuration/appSettings/add[@key='Region']/@value", "[QA_REGION]")
            .FolderPermission("Authenticated Users", FolderRights.Modify)
            .FolderPermission("Users", FolderRights.Modify, "logs")
            .FolderPermission(@"NT AUTHORITY\LocalService", FolderRights.Read, "config")
            .Service("QaDemo.exe", "InstallKitQaDemo", s => s
                .WithDisplayName("InstallKit QA Demo")
                .WithDescription("Service installed by the InstallKit QA pipeline.")))
        .Lifecycle(l => l
            .OnAfterInstall(QaSetup.WriteMode, "MODE")
            .CloseApplication("InstallKitQaClose.exe", terminate: true, timeoutSeconds: 3)
            .RunAfterInstall("QaDemo.exe", "--init [MODE]")
            .RunBeforeUninstall("QaDemo.exe", "--unregister")));

    // A second product whose versions install side by side: 1.0.x and 1.1.x are different lines, so 1.1.0 does not
    // replace 1.0.0, while 1.0.1 replaces 1.0.0. The QA tests install them next to each other.
    plan.Msi(MsiBuilder.Create("InstallKit QA Side")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("a8c2d417-6e3b-4f90-b5d1-7c4e0a92f685") // never change
        .WithOutputFileName("QaSide")
        .WithSideBySide(SideBySideMode.MajorMinor)
        .Content(c => c
            .Payload("QaDemo")
            .Directory(@"..\content\config", "config"))
        .Machine(m => m.Service("QaDemo.exe", "InstallKitQaSide", s => s.WithDisplayName("InstallKit QA Side"))));

    // A second bundle that is only built, not installed: it makes WiX compile the prerequisite helpers (embedded
    // installers, a .NET runtime, WebView2, a download). The "installers" are placeholder files; Burn never runs them here.
    var prerequisites = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "installkit-qa-prerequisites");
    System.IO.Directory.CreateDirectory(prerequisites);
    string Placeholder(string name)
    {
        var path = System.IO.Path.Combine(prerequisites, name);
        System.IO.File.WriteAllText(path, "placeholder for " + name);
        return path;
    }

    plan.Bundle(BundleBuilder.Create("InstallKit QA Prerequisites")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("3d9b2f68-0c47-4a15-8e72-b5a4c19d6f03")
        .WithOutputFileName("QaPrerequisitesSetup")
        .Prerequisites(p => p
            .VcRedist(Placeholder("vc_redist.x64.exe"))
            .VcRedist(PrerequisiteInstaller.FromDownload("https://aka.ms/vs/17/release/vc_redist.x86.exe", new string('A', 128), 13_000_000), PrerequisiteArchitecture.X86)
            .DotNetRuntime(DotNetRuntimeKind.WindowsDesktop, 8, Placeholder("windowsdesktop-runtime-8-win-x64.exe"))
            .WebView2(PrerequisiteInstaller.FromDownload("https://go.microsoft.com/fwlink/p/?LinkId=2124703", new string('B', 128), 1_800_000, "MicrosoftEdgeWebview2Setup.exe")))
        .Packages(k => k.Msi(msi)));

    plan.Bundle(BundleBuilder.Create("InstallKit QA Demo")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("e1a64b07-53c9-4f2d-9e8a-6b0d17c4f2a3")
        .WithOutputFileName("QaDemoSetup")
        .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48))
        .Packages(k => k.Msi(msi)));
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
