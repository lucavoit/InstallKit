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
        .WithFeature("Docs", "Documentation")
        .WithFeature("Samples", "Sample files, not installed unless asked for", installedByDefault: false)
        .WithDirectory(@"..\content\docs", "docs", feature: "Docs")
        .WithDirectory(@"..\content\samples", "samples", feature: "Samples")
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
        .WithXmlValue(@"config\qa.config", "/configuration/appSettings/add[@key='Server']/@value", "[MODE]")
        .WithCloseApplication("InstallKitQaClose.exe", terminate: true, timeoutSeconds: 3)
        .WithFolderPermission("Authenticated Users", FolderRights.Modify)
        .WithFolderPermission("Users", FolderRights.Modify, "logs")
        .WithFolderPermission(@"NT AUTHORITY\LocalService", FolderRights.Read, "config")
        .RunAfterInstall("QaDemo.exe", "--init [MODE]")
        .RunBeforeUninstall("QaDemo.exe", "--unregister")
        .WithService("QaDemo.exe", "InstallKitQaDemo", s => s
            .WithDisplayName("InstallKit QA Demo")
            .WithDescription("Service installed by the InstallKit QA pipeline.")));

    // A second product whose versions install side by side: 1.0.x and 1.1.x are different lines, so 1.1.0 does not
    // replace 1.0.0, while 1.0.1 replaces 1.0.0. The QA tests install them next to each other.
    plan.Msi(MsiBuilder.Create("InstallKit QA Side")
        .WithManufacturer("InstallKit")
        .WithUpgradeCode("a8c2d417-6e3b-4f90-b5d1-7c4e0a92f685") // never change
        .WithOutputFileName("QaSide")
        .WithSideBySide(SideBySideMode.MajorMinor)
        .WithPayload("QaDemo")
        .WithDirectory(@"..\content\config", "config")
        .WithService("QaDemo.exe", "InstallKitQaSide", s => s.WithDisplayName("InstallKit QA Side")));

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
        .WithVcRedist(Placeholder("vc_redist.x64.exe"))
        .WithVcRedist(PrerequisiteInstaller.FromDownload("https://aka.ms/vs/17/release/vc_redist.x86.exe", new string('A', 128), 13_000_000), PrerequisiteArchitecture.X86)
        .WithDotNetRuntime(DotNetRuntimeKind.WindowsDesktop, 8, Placeholder("windowsdesktop-runtime-8-win-x64.exe"))
        .WithWebView2(PrerequisiteInstaller.FromDownload("https://go.microsoft.com/fwlink/p/?LinkId=2124703", new string('B', 128), 1_800_000, "MicrosoftEdgeWebview2Setup.exe"))
        .WithMsi(msi));

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
