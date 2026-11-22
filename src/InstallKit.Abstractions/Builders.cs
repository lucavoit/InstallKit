using System;
using System.Collections.Generic;
using BuilderGenerator;

// The fluent builders. BuilderGenerator generates a With<Property>(value), With<Property>(Func<value>)
// and Without<Property>() method for every settable property, so new model properties get builder
// methods automatically. Only collections and other "add one more" helpers are written by hand here.
//
// Always start from Create(...): the generated Build() assigns every property the builder holds, and a
// bare `new MsiBuilder()` holds default(T) values, which would wipe out the model's defaults
// (for example Ui = Minimal or Shortcuts.StartMenu = true). Create(...) seeds the builder with them.
namespace InstallKit
{
    /// <summary>Fluent builder for <see cref="MsiDefinition"/>.</summary>
    [BuilderFor(typeof(MsiDefinition), includeInternals: true)]
    public partial class MsiBuilder
    {
        /// <summary>Starts an MSI with every default applied.</summary>
        public static MsiBuilder Create(string name) => new MsiBuilder().WithValuesFrom(new MsiDefinition(name));

        // The public surface of the MSI builder: scalar settings are the generated With<Property> methods, everything
        // else is grouped by what it does. A group takes a lambda over a small builder (see BuilderGroups.cs), the same
        // way WithService and the bundle's chain items do.

        /// <summary>Files that are always installed: published projects and directories.</summary>
        public MsiBuilder Content(Action<ContentBuilder> configure)
        {
            configure(new ContentBuilder(this, null));
            return this;
        }

        /// <summary>
        /// An optional part of the product that users can leave out, together with the files it installs. Files that are not
        /// added inside a feature are always installed.
        /// </summary>
        /// <param name="name">The name users see in the feature tree. Also what <c>ADDLOCAL</c> and <c>REMOVE</c> on the command line refer to (without spaces).</param>
        /// <param name="description">Shown under the name in the feature tree.</param>
        /// <param name="content">The files of the feature.</param>
        /// <param name="installedByDefault">Whether the feature is installed when the user does not choose. Default <c>true</c>.</param>
        public MsiBuilder Feature(string name, string? description, Action<ContentBuilder> content, bool installedByDefault = true)
        {
            ListOf(Features, WithFeatures).Add(new OptionalFeature(name, description, installedByDefault));
            content(new ContentBuilder(this, name));
            return this;
        }

        /// <inheritdoc cref="Feature(string, string, Action{ContentBuilder}, bool)"/>
        public MsiBuilder Feature(string name, Action<ContentBuilder> content, bool installedByDefault = true) => Feature(name, null, content, installedByDefault);

        /// <summary>The dialogs and branding of the installer.</summary>
        public MsiBuilder Appearance(Action<AppearanceBuilder> configure)
        {
            configure(new AppearanceBuilder(this));
            return this;
        }

        /// <summary>Changes the installer makes to the machine: registry, environment, firewall, services, permissions and XML edits.</summary>
        public MsiBuilder Machine(Action<MachineBuilder> configure)
        {
            configure(new MachineBuilder(this));
            return this;
        }

        /// <summary>Windows shell integration: file types, URL protocols and shortcuts.</summary>
        public MsiBuilder Shell(Action<ShellBuilder> configure)
        {
            configure(new ShellBuilder(this));
            return this;
        }

        /// <summary>What happens around the install: applications to close, install events and commands to run.</summary>
        public MsiBuilder Lifecycle(Action<LifecycleBuilder> configure)
        {
            configure(new LifecycleBuilder(this));
            return this;
        }

        /// <summary>What the target machine must have: Windows version, 64-bit Windows, or any MSI condition.</summary>
        public MsiBuilder Requirements(Action<RequirementsBuilder> configure)
        {
            configure(new RequirementsBuilder(this));
            return this;
        }

        /// <summary>MSI properties: fixed values, and values the modern dialogs ask the user for.</summary>
        public MsiBuilder Settings(Action<SettingsBuilder> configure)
        {
            configure(new SettingsBuilder(this));
            return this;
        }

        /// <summary>Includes the published output of an <c>&lt;InstallerPayload&gt;</c> project. Pass <paramref name="feature"/> to make these files optional.</summary>
        internal MsiBuilder AddPayload(string payloadName, string? targetSubdirectory = null, string? feature = null)
        {
            ListOf(Payloads, WithPayloads).Add(PayloadSource.FromPayload(payloadName, targetSubdirectory, feature));
            return this;
        }

        /// <summary>Includes every file of a directory (recursively).</summary>
        internal MsiBuilder AddDirectory(string sourceDirectory, string? targetSubdirectory = null, string? feature = null)
        {
            ListOf(Payloads, WithPayloads).Add(PayloadSource.FromDirectory(sourceDirectory, targetSubdirectory, feature));
            return this;
        }

        /// <summary>Installs the executable (relative to the install directory) as a Windows service.</summary>
        internal MsiBuilder AddService(string executable, string serviceName, Func<ServiceBuilder, ServiceBuilder>? configure = null)
        {
            var builder = ServiceBuilder.Create(executable, serviceName);
            ListOf(Services, WithServices).Add((configure?.Invoke(builder) ?? builder).Build());
            return this;
        }

        /// <summary>Writes a registry value on install and removes it on uninstall.</summary>
        /// <param name="hive">The registry root.</param>
        /// <param name="key">The key below the root, for example <c>Software\Contoso\My App</c>.</param>
        /// <param name="name">The value name, or <c>null</c> for the key's default value.</param>
        /// <param name="value">The value. MSI formatted text such as <c>[INSTALLDIR]</c> is expanded.</param>
        /// <param name="kind">The value type.</param>
        internal MsiBuilder AddRegistryValue(RegistryHive hive, string key, string? name, string value, RegistryValueKind kind = RegistryValueKind.String)
        {
            ListOf(RegistryEntries, WithRegistryEntries).Add(new RegistryEntry(hive, key, name, value, kind));
            return this;
        }

        /// <summary>Writes a <c>REG_DWORD</c> registry value on install and removes it on uninstall.</summary>
        internal MsiBuilder AddRegistryValue(RegistryHive hive, string key, string? name, int value) =>
            AddRegistryValue(hive, key, name, value.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.DWord);

        /// <summary>Sets, appends to or prepends to an environment variable. The change is undone on uninstall.</summary>
        internal MsiBuilder AddEnvironmentVariable(string name, string value, EnvironmentVariableAction action = EnvironmentVariableAction.Set)
        {
            ListOf(EnvironmentVariables, WithEnvironmentVariables).Add(new EnvironmentVariableEntry(name, value, action));
            return this;
        }

        /// <summary>Adds a folder to <c>PATH</c> and removes it again on uninstall.</summary>
        /// <param name="path">The folder, for example <c>[INSTALLDIR]bin</c>.</param>
        /// <param name="prepend">Put the folder at the start of <c>PATH</c> instead of the end.</param>
        internal MsiBuilder AddPathEntry(string path, bool prepend = false) =>
            AddEnvironmentVariable("PATH", path, prepend ? EnvironmentVariableAction.Prepend : EnvironmentVariableAction.Append);

        /// <inheritdoc cref="MsiDefinition.AddFileAssociation"/>
        internal MsiBuilder AddFileAssociation(string extension, string description, string? executable = null, string? progId = null)
        {
            ListOf(FileAssociations, WithFileAssociations).Add(new FileAssociation(extension, description, executable, progId));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddUrlProtocol"/>
        internal MsiBuilder AddUrlProtocol(string scheme, string description, string? executable = null)
        {
            ListOf(UrlProtocols, WithUrlProtocols).Add(new UrlProtocol(scheme, description, executable));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddFirewallRule"/>
        internal MsiBuilder AddFirewallRule(string name, string? port = null, FirewallProtocol protocol = FirewallProtocol.Tcp, string? executable = null,
            FirewallScope scope = FirewallScope.Any, FirewallProfile profile = FirewallProfile.All)
        {
            ListOf(FirewallRules, WithFirewallRules).Add(new FirewallRule(name, port, protocol, executable, scope, profile));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddXmlValue"/>
        internal MsiBuilder AddXmlValue(string file, string xpath, string value)
        {
            ListOf(XmlValues, WithXmlValues).Add(new XmlValue(file, xpath, value));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddCloseApplication"/>
        internal MsiBuilder AddCloseApplication(string processName, bool terminate = false, int timeoutSeconds = 5)
        {
            ListOf(CloseApplications, WithCloseApplications).Add(new CloseApplicationRule(processName, terminate, timeoutSeconds));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddLaunchCondition"/>
        internal MsiBuilder AddLaunchCondition(string condition, string message)
        {
            ListOf(LaunchConditions, WithLaunchConditions).Add(new LaunchConditionEntry(condition, message));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddFolderPermission"/>
        internal MsiBuilder AddFolderPermission(string account, FolderRights rights, string? folder = null)
        {
            ListOf(FolderPermissions, WithFolderPermissions).Add(new FolderPermission(account, rights, folder));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.AddPromptedProperty"/>
        internal MsiBuilder AddPromptedProperty(string name, string label, string? defaultValue = null, bool required = false)
        {
            ListOf(PromptedProperties, WithPromptedProperties).Add(new PromptedProperty(name, label, defaultValue, required));
            return this;
        }

        /// <summary>Sets a public MSI property.</summary>
        internal MsiBuilder AddProperty(string name, string value)
        {
            var properties = Properties.Value;
            if (properties == null)
            {
                properties = new Dictionary<string, string>();
                WithProperties(properties);
            }

            properties[name] = value;
            return this;
        }

        internal MsiBuilder SetStartMenuShortcut(bool enabled = true)
        {
            ShortcutsOf().StartMenu = enabled;
            return this;
        }

        internal MsiBuilder SetDesktopShortcut(bool enabled = true)
        {
            ShortcutsOf().Desktop = enabled;
            return this;
        }

        /// <summary>Adds an engine-specific customization. Prefer the engine package's extension methods.</summary>
        public MsiBuilder WithEngineCustomization(object customization)
        {
            ListOf(EngineCustomizations, WithEngineCustomizations).Add(customization);
            return this;
        }

        /// <summary>Uses the native modern dialogs (see <see cref="InstallerUi.Modern"/>).</summary>
        internal MsiBuilder UseModernUi(bool chooseInstallDir = false) =>
            WithUi(chooseInstallDir ? InstallerUi.ModernInstallDir : InstallerUi.Modern);

        /// <summary>Uses the WPF dialogs, for custom XAML dialogs (see <see cref="InstallerUi.ModernWpf"/>).</summary>
        internal MsiBuilder UseWpfUi() => WithUi(InstallerUi.ModernWpf);

        /// <summary>Uses the flat WPF dialogs (see <see cref="InstallerUi.ModernFlat"/>).</summary>
        internal MsiBuilder UseFlatUi() => WithUi(InstallerUi.ModernFlat);

        internal MsiBuilder SetAccentColor(string hexColor)
        {
            BrandingOf().AccentColor = hexColor;
            return this;
        }

        internal MsiBuilder SetTheme(InstallerTheme theme)
        {
            BrandingOf().Theme = theme;
            return this;
        }

        internal MsiBuilder SetBackgroundColor(string hexColor)
        {
            BrandingOf().BackgroundColor = hexColor;
            return this;
        }

        internal MsiBuilder SetTextColor(string hexColor)
        {
            BrandingOf().TextColor = hexColor;
            return this;
        }

        internal MsiBuilder SetLogo(string logoFile)
        {
            BrandingOf().LogoFile = logoFile;
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.OnLoad"/>
        internal MsiBuilder AddOnLoad(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.Load, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.OnBeforeInstall"/>
        internal MsiBuilder AddOnBeforeInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.BeforeInstall, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.OnAfterInstall(Action{InstallEvent}, string[])"/>
        internal MsiBuilder AddOnAfterInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.AfterInstall, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.OnAfterInstall(Action{InstallEvent}, InstallFailureMode, string[])"/>
        internal MsiBuilder AddOnAfterInstall(Action<InstallEvent> handler, InstallFailureMode failureMode, params string[] usesProperties)
        {
            ListOf(EventHandlers, WithEventHandlers).Add(new InstallEventHandler(InstallStage.AfterInstall, handler, usesProperties, failureMode));
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.RunAfterInstall"/>
        internal MsiBuilder AddRunAfterInstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
            AddCommand(new RunCommand(path, arguments, RunCommandTiming.AfterInstall), configure);

        /// <inheritdoc cref="MsiDefinition.RunBeforeUninstall"/>
        internal MsiBuilder AddRunBeforeUninstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
            AddCommand(new RunCommand(path, arguments, RunCommandTiming.BeforeUninstall), configure);

        MsiBuilder AddHandler(InstallStage stage, Action<InstallEvent> handler, string[] usesProperties)
        {
            ListOf(EventHandlers, WithEventHandlers).Add(new InstallEventHandler(stage, handler, usesProperties));
            return this;
        }

        MsiBuilder AddCommand(RunCommand command, Action<RunCommand>? configure)
        {
            configure?.Invoke(command);
            ListOf(Commands, WithCommands).Add(command);
            return this;
        }

        InstallKit.Branding BrandingOf()
        {
            var branding = Branding.Value;
            if (branding == null)
            {
                branding = new InstallKit.Branding();
                WithBranding(branding);
            }

            return branding;
        }

        // Type names are qualified below: the generated builder has fields named like the model's properties.
        InstallKit.ShortcutOptions ShortcutsOf()
        {
            var options = Shortcuts.Value;
            if (options == null)
            {
                options = new InstallKit.ShortcutOptions();
                WithShortcuts(options);
            }

            return options;
        }

        internal static IList<T> ListOf<T>(Lazy<IList<T>> current, Func<IList<T>, object> set)
        {
            var list = current.Value;
            if (list == null)
            {
                list = new List<T>();
                set(list);
            }

            return list;
        }
    }

    /// <summary>Fluent builder for <see cref="BundleDefinition"/>.</summary>
    [BuilderFor(typeof(BundleDefinition), includeInternals: true)]
    public partial class BundleBuilder
    {
        /// <summary>Starts a bundle with every default applied.</summary>
        public static BundleBuilder Create(string name) => new BundleBuilder().WithValuesFrom(new BundleDefinition(name));

        // Scalar settings are the generated With<Property> methods. The chain is built with two groups: prerequisites
        // (installed when missing) and packages (your MSIs and executables). Items are chained in the order they are added,
        // so call Prerequisites before Packages to install them first.

        /// <summary>Software that has to be on the machine before the packages: .NET, the Visual C++ runtime, WebView2.</summary>
        public BundleBuilder Prerequisites(Action<PrerequisitesBuilder> configure)
        {
            configure(new PrerequisitesBuilder(ChainOf()));
            return this;
        }

        /// <summary>The MSIs and executables the bundle installs.</summary>
        public BundleBuilder Packages(Action<PackagesBuilder> configure)
        {
            configure(new PackagesBuilder(ChainOf()));
            return this;
        }

        /// <summary>The logo and other branding of the setup window.</summary>
        public BundleBuilder Appearance(Action<BundleAppearanceBuilder> configure)
        {
            configure(new BundleAppearanceBuilder(this));
            return this;
        }

        /// <summary>Adds an engine-specific customization. Prefer the engine package's extension methods.</summary>
        public BundleBuilder WithEngineCustomization(object customization)
        {
            MsiBuilder.ListOf(EngineCustomizations, WithEngineCustomizations).Add(customization);
            return this;
        }

        internal BundleBuilder SetLogo(string logoFile)
        {
            BrandingOf().LogoFile = logoFile;
            return this;
        }

        internal BundleBuilder Brand(Action<InstallKit.Branding> change)
        {
            change(BrandingOf());
            return this;
        }

        InstallKit.Branding BrandingOf()
        {
            var branding = Branding.Value;
            if (branding == null)
            {
                branding = new InstallKit.Branding();
                WithBranding(branding);
            }

            return branding;
        }

        BundleChain ChainOf()
        {
            var chain = Chain.Value;
            if (chain == null)
            {
                chain = new BundleChain();
                WithChain(chain);
            }

            return chain;
        }
    }

    /// <summary>Fluent builder for <see cref="ServiceDefinition"/>.</summary>
    [BuilderFor(typeof(ServiceDefinition), includeInternals: true)]
    public partial class ServiceBuilder
    {
        /// <summary>Starts a service with every default applied.</summary>
        public static ServiceBuilder Create(string executable, string serviceName) =>
            new ServiceBuilder().WithValuesFrom(new ServiceDefinition(executable, serviceName));
    }
}
