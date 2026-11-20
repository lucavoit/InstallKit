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

        /// <summary>Classic WiX: welcome / license / progress.</summary>
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

        /// <summary><see cref="Modern"/> with the install folder (and a Browse button) on the page after the welcome page.</summary>
        ModernInstallDir,

        /// <summary>
        /// WPF dialogs, which allow custom XAML dialogs. Needs .NET Framework 4.8 on the target machine and
        /// an engine with <see cref="InstallerCapabilities.WpfUi"/>; others fall back to <see cref="Modern"/>.
        /// </summary>
        ModernWpf,

        /// <summary>
        /// A flat, borderless-looking dark UI drawn with WPF: one page per step with big titles, flat accent buttons and a
        /// custom checkbox and path box, in your theme and colours. Shows the license, install folder, prompted values and
        /// optional features, and repair/remove. Needs .NET Framework 4.8 on the target machine and an engine with
        /// <see cref="InstallerCapabilities.WpfUi"/>; others fall back to <see cref="ModernInstallDir"/>.
        /// </summary>
        ModernFlat,

        /// <summary>
        /// What nothing was chosen for, and the default of every MSI: the best UI the engine offers. The WixSharp engine shows the
        /// flat WPF dialogs (<see cref="ModernFlat"/>), the WixSharp.Core engine the native modern dialogs with the install folder
        /// (<see cref="ModernInstallDir"/>), which need nothing on the target machine. Choose a UI yourself to pin it.
        /// </summary>
        Default,
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
