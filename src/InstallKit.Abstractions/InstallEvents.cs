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

        /// <summary>Fails the install and rolls it back.</summary>
        public void Cancel(string? reason = null)
        {
            IsCanceled = true;
            CancelReason = reason;
        }

        public bool IsCanceled { get; private set; }

        public string? CancelReason { get; private set; }
    }

    /// <summary>A handler registered with <see cref="MsiDefinition.OnAfterInstall"/> and friends.</summary>
    public sealed class InstallEventHandler
    {
        public InstallEventHandler(InstallStage stage, Action<InstallEvent> handler, IEnumerable<string>? usesProperties = null)
        {
            Stage = stage;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            UsesProperties = (usesProperties ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray();
        }

        public InstallStage Stage { get; }

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
