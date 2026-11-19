using System;

// The groups of the fluent builders. MsiBuilder and BundleBuilder keep the scalar settings (name, manufacturer,
// upgrade code, ...) as generated With<Property> methods and hand the rest to these small builders, one per area:
//
//   MsiBuilder.Create("App")
//       .WithManufacturer("Contoso").WithUpgradeCode("...")
//       .Content(c => c.Payload("App"))
//       .Feature("Docs", "Documentation", f => f.Directory(@"..\docs", "docs"))
//       .Appearance(a => a.Modern().AccentColor("#3B82F6"))
//       .Machine(m => m.PathEntry("[INSTALLDIR]bin").Service("App.exe", "App"))
//       .Shell(s => s.FileAssociation(".app", "App file"))
//       .Lifecycle(l => l.CloseApplication("App.exe").OnAfterInstall(Setup.Configure))
//       .Settings(s => s.Property("MODE", "client"));
//
// The groups only forward to MsiBuilder's internal Add/Set methods, so a group cannot do anything the model could not.
namespace InstallKit
{
    /// <summary>
    /// Files to install: the published output of an <c>&lt;InstallerPayload&gt;</c> project, or a directory. Used by
    /// <see cref="MsiBuilder.Content"/> for files that are always installed, and by <see cref="MsiBuilder.Feature(string, string, Action{ContentBuilder}, bool)"/>
    /// for the files of an optional feature.
    /// </summary>
    public sealed class ContentBuilder
    {
        readonly MsiBuilder _msi;
        readonly string? _feature;

        internal ContentBuilder(MsiBuilder msi, string? feature)
        {
            _msi = msi;
            _feature = feature;
        }

        /// <summary>Includes the published output of an <c>&lt;InstallerPayload&gt;</c> project.</summary>
        /// <param name="payloadName">The payload name (the project file name unless the item sets <c>Name</c>).</param>
        /// <param name="targetSubdirectory">The folder below the install directory, or <c>null</c> for the install directory itself.</param>
        public ContentBuilder Payload(string payloadName, string? targetSubdirectory = null)
        {
            _msi.AddPayload(payloadName, targetSubdirectory, _feature);
            return this;
        }

        /// <summary>Includes every file of a directory (recursively).</summary>
        /// <param name="sourceDirectory">The directory, relative to the installer project.</param>
        /// <param name="targetSubdirectory">The folder below the install directory, or <c>null</c> for the install directory itself.</param>
        public ContentBuilder Directory(string sourceDirectory, string? targetSubdirectory = null)
        {
            _msi.AddDirectory(sourceDirectory, targetSubdirectory, _feature);
            return this;
        }
    }

    /// <summary>The dialogs and branding of an MSI. Used by <see cref="MsiBuilder.Appearance"/>.</summary>
    public sealed class AppearanceBuilder
    {
        readonly MsiBuilder _msi;

        internal AppearanceBuilder(MsiBuilder msi) => _msi = msi;

        /// <summary>Uses the native modern dialogs (see <see cref="InstallerUi.Modern"/>).</summary>
        /// <param name="chooseInstallDir">Show the install folder and a Browse button on the welcome page.</param>
        public AppearanceBuilder Modern(bool chooseInstallDir = false)
        {
            _msi.UseModernUi(chooseInstallDir);
            return this;
        }

        /// <summary>Uses the WPF dialogs, for custom XAML dialogs (see <see cref="InstallerUi.ModernWpf"/>).</summary>
        public AppearanceBuilder Wpf()
        {
            _msi.UseWpfUi();
            return this;
        }

        /// <summary>Uses one of the classic WiX dialog sets (for example <see cref="InstallerUi.FeatureTree"/>).</summary>
        public AppearanceBuilder Classic(InstallerUi ui)
        {
            _msi.WithUi(ui);
            return this;
        }

        /// <summary>The accent color of the generated dialog images, as <c>#RRGGBB</c>.</summary>
        public AppearanceBuilder AccentColor(string hexColor)
        {
            _msi.SetAccentColor(hexColor);
            return this;
        }

        /// <summary>Dark (the default) or light pages in the native modern dialogs.</summary>
        public AppearanceBuilder Theme(InstallerTheme theme)
        {
            _msi.SetTheme(theme);
            return this;
        }

        /// <summary>The page background of the modern dialogs, as <c>#RRGGBB</c>. Defaults to the theme's.</summary>
        public AppearanceBuilder BackgroundColor(string hexColor)
        {
            _msi.SetBackgroundColor(hexColor);
            return this;
        }

        /// <summary>The text colour of the modern dialogs, as <c>#RRGGBB</c>. Defaults to the theme's.</summary>
        public AppearanceBuilder TextColor(string hexColor)
        {
            _msi.SetTextColor(hexColor);
            return this;
        }

        /// <summary>The logo shown in the dialogs.</summary>
        public AppearanceBuilder Logo(string logoFile)
        {
            _msi.SetLogo(logoFile);
            return this;
        }
    }

    /// <summary>Changes an MSI makes to the machine. Used by <see cref="MsiBuilder.Machine"/>.</summary>
    public sealed class MachineBuilder
    {
        readonly MsiBuilder _msi;

        internal MachineBuilder(MsiBuilder msi) => _msi = msi;

        /// <summary>Writes a registry value on install and removes it on uninstall.</summary>
        /// <param name="hive">The registry root.</param>
        /// <param name="key">The key below the root, for example <c>Software\Contoso\My App</c>.</param>
        /// <param name="name">The value name, or <c>null</c> for the key's default value.</param>
        /// <param name="value">The value. MSI formatted text such as <c>[INSTALLDIR]</c> is expanded.</param>
        /// <param name="kind">The value type.</param>
        public MachineBuilder Registry(RegistryHive hive, string key, string? name, string value, RegistryValueKind kind = RegistryValueKind.String)
        {
            _msi.AddRegistryValue(hive, key, name, value, kind);
            return this;
        }

        /// <summary>Writes a <c>REG_DWORD</c> registry value on install and removes it on uninstall.</summary>
        public MachineBuilder Registry(RegistryHive hive, string key, string? name, int value)
        {
            _msi.AddRegistryValue(hive, key, name, value);
            return this;
        }

        /// <summary>Sets, appends to or prepends to an environment variable. The change is undone on uninstall.</summary>
        public MachineBuilder EnvironmentVariable(string name, string value, EnvironmentVariableAction action = EnvironmentVariableAction.Set)
        {
            _msi.AddEnvironmentVariable(name, value, action);
            return this;
        }

        /// <summary>Adds a folder to <c>PATH</c> and removes it again on uninstall.</summary>
        /// <param name="path">The folder, for example <c>[INSTALLDIR]bin</c>.</param>
        /// <param name="prepend">Put the folder at the start of <c>PATH</c> instead of the end.</param>
        public MachineBuilder PathEntry(string path, bool prepend = false)
        {
            _msi.AddPathEntry(path, prepend);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddFirewallRule"/>
        public MachineBuilder FirewallRule(string name, string? port = null, FirewallProtocol protocol = FirewallProtocol.Tcp, string? executable = null,
            FirewallScope scope = FirewallScope.Any, FirewallProfile profile = FirewallProfile.All)
        {
            _msi.AddFirewallRule(name, port, protocol, executable, scope, profile);
            return this;
        }

        /// <summary>Installs the executable (relative to the install directory) as a Windows service.</summary>
        public MachineBuilder Service(string executable, string serviceName, Func<ServiceBuilder, ServiceBuilder>? configure = null)
        {
            _msi.AddService(executable, serviceName, configure);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddFolderPermission"/>
        public MachineBuilder FolderPermission(string account, FolderRights rights, string? folder = null)
        {
            _msi.AddFolderPermission(account, rights, folder);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddXmlValue"/>
        public MachineBuilder XmlValue(string file, string xpath, string value)
        {
            _msi.AddXmlValue(file, xpath, value);
            return this;
        }
    }

    /// <summary>Windows shell integration of an MSI. Used by <see cref="MsiBuilder.Shell"/>.</summary>
    public sealed class ShellBuilder
    {
        readonly MsiBuilder _msi;

        internal ShellBuilder(MsiBuilder msi) => _msi = msi;

        /// <inheritdoc cref="MsiDefinition.AddFileAssociation"/>
        public ShellBuilder FileAssociation(string extension, string description, string? executable = null, string? progId = null)
        {
            _msi.AddFileAssociation(extension, description, executable, progId);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddUrlProtocol"/>
        public ShellBuilder UrlProtocol(string scheme, string description, string? executable = null)
        {
            _msi.AddUrlProtocol(scheme, description, executable);
            return this;
        }

        /// <summary>Whether the Start menu gets a shortcut to the main executable. On by default.</summary>
        public ShellBuilder StartMenuShortcut(bool enabled = true)
        {
            _msi.SetStartMenuShortcut(enabled);
            return this;
        }

        /// <summary>Whether the desktop gets a shortcut to the main executable. Off by default.</summary>
        public ShellBuilder DesktopShortcut(bool enabled = true)
        {
            _msi.SetDesktopShortcut(enabled);
            return this;
        }
    }

    /// <summary>What happens around an install. Used by <see cref="MsiBuilder.Lifecycle"/>.</summary>
    public sealed class LifecycleBuilder
    {
        readonly MsiBuilder _msi;

        internal LifecycleBuilder(MsiBuilder msi) => _msi = msi;

        /// <inheritdoc cref="MsiDefinition.AddCloseApplication"/>
        public LifecycleBuilder CloseApplication(string processName, bool terminate = false, int timeoutSeconds = 5)
        {
            _msi.AddCloseApplication(processName, terminate, timeoutSeconds);
            return this;
        }

        // The install event handlers are found by name by the InstallKit generator (OnLoad, OnBeforeInstall, OnAfterInstall
        // on this type), which turns the static method into a custom action. Keep the names.

        /// <inheritdoc cref="MsiDefinition.OnLoad"/>
        public LifecycleBuilder OnLoad(Action<InstallEvent> handler, params string[] usesProperties)
        {
            _msi.AddOnLoad(handler, usesProperties);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.OnBeforeInstall"/>
        public LifecycleBuilder OnBeforeInstall(Action<InstallEvent> handler, params string[] usesProperties)
        {
            _msi.AddOnBeforeInstall(handler, usesProperties);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.OnAfterInstall"/>
        public LifecycleBuilder OnAfterInstall(Action<InstallEvent> handler, params string[] usesProperties)
        {
            _msi.AddOnAfterInstall(handler, usesProperties);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.RunAfterInstall"/>
        public LifecycleBuilder RunAfterInstall(string path, string? arguments = null, Action<RunCommand>? configure = null)
        {
            _msi.AddRunAfterInstall(path, arguments, configure);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.RunBeforeUninstall"/>
        public LifecycleBuilder RunBeforeUninstall(string path, string? arguments = null, Action<RunCommand>? configure = null)
        {
            _msi.AddRunBeforeUninstall(path, arguments, configure);
            return this;
        }
    }

    /// <summary>MSI properties. Used by <see cref="MsiBuilder.Settings"/>.</summary>
    public sealed class SettingsBuilder
    {
        readonly MsiBuilder _msi;

        internal SettingsBuilder(MsiBuilder msi) => _msi = msi;

        /// <summary>Sets a public MSI property. It can be overridden on the <c>msiexec</c> command line.</summary>
        public SettingsBuilder Property(string name, string value)
        {
            _msi.AddProperty(name, value);
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddPromptedProperty"/>
        public SettingsBuilder Prompt(string name, string label, string? defaultValue = null, bool required = false)
        {
            _msi.AddPromptedProperty(name, label, defaultValue, required);
            return this;
        }
    }

    /// <summary>The prerequisites of a bundle. Used by <see cref="BundleBuilder.Prerequisites"/>.</summary>
    public sealed class PrerequisitesBuilder
    {
        readonly BundleChain _chain;

        internal PrerequisitesBuilder(BundleChain chain) => _chain = chain;

        /// <summary>Installs a .NET Framework redistributable when it is missing.</summary>
        public PrerequisitesBuilder NetFramework(NetFrameworkVersion version)
        {
            _chain.NetFramework(version);
            return this;
        }

        /// <inheritdoc cref="BundleChain.VcRedist"/>
        public PrerequisitesBuilder VcRedist(PrerequisiteInstaller installer, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64)
        {
            _chain.VcRedist(installer, architecture);
            return this;
        }

        /// <summary>Installs the Visual C++ Redistributable from an installer file that is embedded into the setup.</summary>
        public PrerequisitesBuilder VcRedist(string installerPath, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64) =>
            VcRedist(PrerequisiteInstaller.FromFile(installerPath), architecture);

        /// <inheritdoc cref="BundleChain.DotNetRuntime"/>
        public PrerequisitesBuilder DotNetRuntime(DotNetRuntimeKind kind, int majorVersion, PrerequisiteInstaller installer, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64)
        {
            _chain.DotNetRuntime(kind, majorVersion, installer, architecture);
            return this;
        }

        /// <summary>Installs a .NET runtime from an installer file that is embedded into the setup.</summary>
        public PrerequisitesBuilder DotNetRuntime(DotNetRuntimeKind kind, int majorVersion, string installerPath, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64) =>
            DotNetRuntime(kind, majorVersion, PrerequisiteInstaller.FromFile(installerPath), architecture);

        /// <inheritdoc cref="BundleChain.WebView2"/>
        public PrerequisitesBuilder WebView2(PrerequisiteInstaller installer)
        {
            _chain.WebView2(installer);
            return this;
        }

        /// <summary>Installs the WebView2 Runtime from an installer file that is embedded into the setup.</summary>
        public PrerequisitesBuilder WebView2(string installerPath) => WebView2(PrerequisiteInstaller.FromFile(installerPath));
    }

    /// <summary>The packages of a bundle. Used by <see cref="BundleBuilder.Packages"/>.</summary>
    public sealed class PackagesBuilder
    {
        readonly BundleChain _chain;

        internal PackagesBuilder(BundleChain chain) => _chain = chain;

        /// <summary>Chains an MSI defined in the same plan; it is built first.</summary>
        public PackagesBuilder Msi(MsiDefinition msi, Action<MsiChainItem>? configure = null)
        {
            _chain.Msi(msi, configure);
            return this;
        }

        /// <summary>Chains an existing MSI file.</summary>
        public PackagesBuilder Msi(string msiPath, Action<MsiChainItem>? configure = null)
        {
            _chain.Msi(msiPath, configure);
            return this;
        }

        /// <summary>Chains an executable installer. Set a DetectCondition so it is skipped when present.</summary>
        public PackagesBuilder Exe(string exePath, Action<ExeChainItem>? configure = null)
        {
            _chain.Exe(exePath, configure);
            return this;
        }
    }

    /// <summary>The look of a bundle. Used by <see cref="BundleBuilder.Appearance"/>.</summary>
    public sealed class BundleAppearanceBuilder
    {
        readonly BundleBuilder _bundle;

        internal BundleAppearanceBuilder(BundleBuilder bundle) => _bundle = bundle;

        /// <summary>The logo shown in the setup window.</summary>
        public BundleAppearanceBuilder Logo(string logoFile)
        {
            _bundle.SetLogo(logoFile);
            return this;
        }

        /// <summary>Dark (the default) or light setup window.</summary>
        public BundleAppearanceBuilder Theme(InstallerTheme theme)
        {
            _bundle.Brand(b => b.Theme = theme);
            return this;
        }

        /// <summary>The accent colour (window headings), as <c>#RRGGBB</c>.</summary>
        public BundleAppearanceBuilder AccentColor(string hexColor)
        {
            _bundle.Brand(b => b.AccentColor = hexColor);
            return this;
        }

        /// <summary>The window background, as <c>#RRGGBB</c>. Defaults to the theme's.</summary>
        public BundleAppearanceBuilder BackgroundColor(string hexColor)
        {
            _bundle.Brand(b => b.BackgroundColor = hexColor);
            return this;
        }

        /// <summary>The text colour, as <c>#RRGGBB</c>. Defaults to the theme's.</summary>
        public BundleAppearanceBuilder TextColor(string hexColor)
        {
            _bundle.Brand(b => b.TextColor = hexColor);
            return this;
        }
    }
}
