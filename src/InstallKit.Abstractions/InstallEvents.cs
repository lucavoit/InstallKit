using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InstallKit
{
    /// <summary>When an install event handler runs.</summary>
    public enum InstallStage
    {
        /// <summary>When the MSI session starts, before searches run. Not elevated.</summary>
        Load,

        /// <summary>Before files are copied. Not elevated.</summary>
        BeforeInstall,

        /// <summary>After files are copied. Runs elevated (deferred) for per-machine installs.</summary>
        AfterInstall,
    }

    /// <summary>
    /// What an install event handler sees on the target machine. Engine-neutral: each engine adapts
    /// its own runtime (WixSharp's SetupEventArgs today) to this type.
    /// </summary>
    public abstract class InstallEvent
    {
        public abstract InstallStage Stage { get; }

        /// <summary>A fresh install (also true for a major upgrade's new version).</summary>
        public abstract bool IsInstalling { get; }

        public abstract bool IsUninstalling { get; }

        public abstract bool IsUpgrading { get; }

        public abstract bool IsRepairing { get; }

        /// <summary>A first install of the product: not an upgrade, a repair, a change or an uninstall.</summary>
        public virtual bool IsFreshInstall => IsInstalling && !IsUpgrading;

        /// <summary>
        /// The installed product is being changed (for example optional features added or removed with Change, or
        /// <c>ADDLOCAL</c>/<c>REMOVE</c> on the command line): not a repair, an upgrade or an uninstall.
        /// </summary>
        public virtual bool IsChange => false;

        /// <summary>The version of the MSI that is being installed (for example <c>1.2.3.0</c>), when known.</summary>
        public virtual string? ProductVersion => null;

        /// <summary>
        /// Whether the optional feature (the name given to <c>Feature(...)</c>) is installed once this install is done:
        /// ticked in a fresh install, kept or added in a change, and false after it is removed or the product is uninstalled.
        /// </summary>
        public virtual bool IsFeatureSelected(string feature) => false;

        /// <summary>Whether the optional feature was installed before this install started. Compare with <see cref="IsFeatureSelected"/> to see what a change adds or removes.</summary>
        public virtual bool WasFeatureSelected(string feature) => false;

        /// <summary>
        /// The Windows user name of the account that runs the setup (MSI's <c>LogonUser</c>), or <c>null</c> when unknown.
        /// When a standard user is elevated with another administrator's credentials this is the administrator, not the person at the keyboard.
        /// </summary>
        public virtual string? InstallingUserName => null;

        /// <summary>The SID of the account that runs the setup (MSI's <c>UserSID</c>), or <c>null</c> when unknown.</summary>
        public virtual string? InstallingUserSid => null;

        /// <summary>
        /// The setup runs as <c>LocalSystem</c> (a deployment tool such as SCCM or Intune), so there is no interactive user to configure:
        /// <see cref="InstallingUserSid"/> is <c>S-1-5-18</c>.
        /// </summary>
        public virtual bool IsInstalledBySystem => InstallingUserSid == "S-1-5-18";

        /// <summary>The install runs without UI (for example <c>msiexec /qn</c>).</summary>
        public abstract bool IsUiSuppressed { get; }

        /// <summary>The install directory with a trailing backslash, when known.</summary>
        public abstract string? InstallDir { get; }

        /// <summary>
        /// Reads an MSI property. In <see cref="InstallStage.AfterInstall"/> only the properties named in
        /// <c>usesProperties</c> (plus INSTALLDIR) are available.
        /// </summary>
        public abstract string? this[string property] { get; }

        /// <summary>Writes to the MSI log (<c>msiexec /l*v</c>).</summary>
        public abstract void Log(string message);

        /// <summary>Fails the install and rolls it back. The reason is written to the log and shown to the user.</summary>
        public void Cancel(string? reason = null)
        {
            IsCanceled = true;
            CancelReason = reason;
        }

        /// <summary>
        /// Fails the install and rolls it back, with a message the user sees (in the setup dialog, or as the error of a silent install in
        /// the log) and that is written to the MSI log. Same as <see cref="Cancel"/>, and usable in every stage.
        /// </summary>
        public void Fail(string message) => Cancel(message);

        public bool IsCanceled { get; private set; }

        public string? CancelReason { get; private set; }
    }

    /// <summary>What happens when an install event handler throws or calls <see cref="InstallEvent.Fail"/>.</summary>
    public enum InstallFailureMode
    {
        /// <summary>The install fails and is rolled back. The default.</summary>
        Rollback,

        /// <summary>The problem is written to the MSI log and the install carries on. For advisory work such as optional configuration.</summary>
        Warn,
    }

    /// <summary>A handler registered with <see cref="MsiDefinition.OnAfterInstall(Action{InstallEvent}, string[])"/> and friends.</summary>
    public sealed class InstallEventHandler
    {
        public InstallEventHandler(InstallStage stage, Action<InstallEvent> handler, IEnumerable<string>? usesProperties = null,
            InstallFailureMode failureMode = InstallFailureMode.Rollback)
        {
            FailureMode = failureMode;
            Stage = stage;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            UsesProperties = (usesProperties ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray();
        }

        public InstallStage Stage { get; }

        /// <summary>Whether a failure of this handler rolls the install back or is only logged.</summary>
        public InstallFailureMode FailureMode { get; }

        /// <summary>
        /// Runs on the target machine, not during the build. It must be a static method passed as a
        /// method group (<c>.Lifecycle(l => l.OnAfterInstall(Setup.Configure))</c>): the InstallKit source generator turns it
        /// into a custom action entry point, which lambdas and instance methods cannot be.
        /// </summary>
        public Action<InstallEvent> Handler { get; }

        /// <summary>MSI properties the handler reads (needed for <see cref="InstallStage.AfterInstall"/>).</summary>
        public IReadOnlyList<string> UsesProperties { get; }

        /// <summary>Why the handler cannot run at install time, or <c>null</c> when it can.</summary>
        public string? GetInstallTimeProblem()
        {
            var invocations = Handler.GetInvocationList();
            if (invocations.Length != 1)
                return "it combines several delegates; register each method separately";

            var method = invocations[0].Method;
            if (!method.IsStatic || method.Name.IndexOf('<') >= 0)
                return "it is a lambda, local function or instance method. Pass a static method instead, for example .Lifecycle(l => l.OnAfterInstall(Setup.Configure))";

            return null;
        }
    }

    /// <summary>When a <see cref="RunCommand"/> runs.</summary>
    public enum RunCommandTiming
    {
        /// <summary>After files are installed, on a fresh install or upgrade.</summary>
        AfterInstall,

        /// <summary>Before files are removed, on a real uninstall (not during an upgrade).</summary>
        BeforeUninstall,
    }

    /// <summary>Runs a program during the install, for example to initialise data or register something.</summary>
    public sealed class RunCommand
    {
        public RunCommand(string path, string? arguments, RunCommandTiming timing)
        {
            Path = path;
            Arguments = arguments;
            Timing = timing;
        }

        /// <summary>
        /// The program. A relative path is relative to the install directory; MSI formatted values
        /// such as <c>[System64Folder]cmd.exe</c> are passed through.
        /// </summary>
        public string Path { get; }

        public string? Arguments { get; }

        public RunCommandTiming Timing { get; }

        /// <summary>Run with elevation (as LocalSystem for per-machine installs). Default <c>true</c>.</summary>
        public bool Elevated { get; set; } = true;

        /// <summary>Fail the install on a non-zero exit code. Default <c>true</c>.</summary>
        public bool CheckExitCode { get; set; } = true;
    }

    /// <summary>Colour scheme of the modern dialogs (<see cref="InstallerUi.Modern"/>, <see cref="InstallerUi.ModernFlat"/>) and the bundle window.</summary>
    public enum InstallerTheme
    {
        /// <summary>Dark pages with a soft accent glow. The default.</summary>
        Dark = 0,

        /// <summary>White pages with an accent side panel.</summary>
        Light = 1,

        /// <summary>
        /// Follows the Windows setting of the person running the installer (Settings, Personalization, Colors, "Choose your
        /// default app mode"): light or dark, decided when the setup starts. Only the flat WPF UI
        /// (<see cref="InstallerUi.ModernFlat"/>) can do that: the native dialogs and the bundle window draw their colours when
        /// they are built, so they use <see cref="Dark"/> and the build prints warning IK204.
        /// </summary>
        Auto = 2,
    }

    /// <summary>Look of the installer UI. Applies to classic and modern dialogs and the bundle.</summary>
    public sealed class Branding
    {
        /// <summary>Colour scheme of the modern dialogs. Defaults to <see cref="InstallerTheme.Dark"/>.</summary>
        public InstallerTheme Theme { get; set; }

        /// <summary>Accent colour as <c>#RRGGBB</c>: side panel glow, banner line, headings of the bundle. Defaults to a neutral blue.</summary>
        public string? AccentColor { get; set; }

        /// <summary>
        /// Page background as <c>#RRGGBB</c>. Defaults to near-black for <see cref="InstallerTheme.Dark"/> and white for
        /// <see cref="InstallerTheme.Light"/>. Applies to the modern dialogs and the bundle window.
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Text colour as <c>#RRGGBB</c>. Defaults to match the theme. Secondary text is derived from it and the
        /// background. Applies to the modern dialogs and the bundle window.
        /// </summary>
        public string? TextColor { get; set; }

        /// <summary>Logo (.png recommended; .ico, .bmp and .jpg also work). Shown in dialogs and the bundle.</summary>
        public string? LogoFile { get; set; }

        /// <summary>Use your own 493×58 banner bitmap instead of the generated one.</summary>
        public string? BannerImage { get; set; }

        /// <summary>Use your own 493×312 dialog bitmap instead of the generated one.</summary>
        public string? BackgroundImage { get; set; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(AccentColor) && string.IsNullOrWhiteSpace(LogoFile) &&
            string.IsNullOrWhiteSpace(BannerImage) && string.IsNullOrWhiteSpace(BackgroundImage);

        public const string DefaultAccentColor = "#0067C0";
    }

    /// <summary>Optional engine features. The host checks them so an engine can fall back gracefully.</summary>
    [Flags]
    public enum InstallerCapabilities
    {
        None = 0,

        /// <summary>Install event handlers (<see cref="InstallEventHandler"/>).</summary>
        InstallEvents = 1,

        /// <summary>The native modern dialog set (<see cref="InstallerUi.Modern"/>).</summary>
        ModernUi = 2,

        /// <summary>Generated branded dialog images.</summary>
        BrandedImages = 4,

        /// <summary>WPF dialogs and custom XAML dialogs (<see cref="InstallerUi.ModernWpf"/>).</summary>
        WpfUi = 8,
    }
}
