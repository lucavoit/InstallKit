namespace InstallKit
{
    /// <summary>Who the MSI is installed for.</summary>
    public enum InstallScope
    {
        /// <summary>All users. Requires elevation. Default.</summary>
        PerMachine,

        /// <summary>The current user only.</summary>
        PerUser,
    }

    /// <summary>Target processor architecture of the MSI.</summary>
    public enum InstallerPlatform
    {
        X64,
        X86,
        Arm64,
    }

    /// <summary>The built-in dialog set shown by the MSI.</summary>
    public enum InstallerUi
    {
        /// <summary>Progress bar only.</summary>
        ProgressOnly,

        /// <summary>Welcome / license / progress. Default.</summary>
        Minimal,

        /// <summary>Adds a page to choose the install folder.</summary>
        InstallDir,

        /// <summary>Adds feature selection.</summary>
        FeatureTree,

        /// <summary>
        /// Modern native dialogs: one branded welcome page (with the license when set), progress and finish,
        /// plus repair/remove. Built from MSI's own dialog tables, so the target machine needs nothing extra.
        /// </summary>
        Modern,

        /// <summary><see cref="Modern"/> with the install folder (and a Browse button) on the welcome page.</summary>
        ModernInstallDir,

        /// <summary>
        /// WPF dialogs, which allow custom XAML dialogs. Needs .NET Framework 4.8 on the target machine and
        /// an engine with <see cref="InstallerCapabilities.WpfUi"/>; others fall back to <see cref="Modern"/>.
        /// </summary>
        ModernWpf,
    }

    /// <summary>How a Windows service is started.</summary>
    public enum ServiceStartMode
    {
        Automatic,
        Manual,
        Disabled,
    }

    /// <summary>.NET Framework redistributables a bundle can chain in.</summary>
    public enum NetFrameworkVersion
    {
        NetFx462,
        NetFx472,
        NetFx48,
        NetFx481,
    }
}
