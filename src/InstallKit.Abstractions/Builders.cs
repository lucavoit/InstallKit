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

        /// <summary>Includes the published output of an <c>&lt;InstallerPayload&gt;</c> project.</summary>
        public MsiBuilder WithPayload(string payloadName, string? targetSubdirectory = null)
        {
            ListOf(Payloads, WithPayloads).Add(PayloadSource.FromPayload(payloadName, targetSubdirectory));
            return this;
        }

        /// <summary>Includes every file of a directory (recursively).</summary>
        public MsiBuilder WithDirectory(string sourceDirectory, string? targetSubdirectory = null)
        {
            ListOf(Payloads, WithPayloads).Add(PayloadSource.FromDirectory(sourceDirectory, targetSubdirectory));
            return this;
        }

        /// <summary>Installs the executable (relative to the install directory) as a Windows service.</summary>
        public MsiBuilder WithService(string executable, string serviceName, Func<ServiceBuilder, ServiceBuilder>? configure = null)
        {
            var builder = ServiceBuilder.Create(executable, serviceName);
            ListOf(Services, WithServices).Add((configure?.Invoke(builder) ?? builder).Build());
            return this;
        }

        /// <summary>Sets a public MSI property.</summary>
        public MsiBuilder WithProperty(string name, string value)
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

        public MsiBuilder WithStartMenuShortcut(bool enabled = true)
        {
            ShortcutsOf().StartMenu = enabled;
            return this;
        }

        public MsiBuilder WithDesktopShortcut(bool enabled = true)
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
        public MsiBuilder WithModernUi(bool chooseInstallDir = false) =>
            WithUi(chooseInstallDir ? InstallerUi.ModernInstallDir : InstallerUi.Modern);

        /// <summary>Uses the WPF dialogs, for custom XAML dialogs (see <see cref="InstallerUi.ModernWpf"/>).</summary>
        public MsiBuilder WithWpfUi() => WithUi(InstallerUi.ModernWpf);

        public MsiBuilder WithAccentColor(string hexColor)
        {
            BrandingOf().AccentColor = hexColor;
            return this;
        }

        public MsiBuilder WithLogo(string logoFile)
        {
            BrandingOf().LogoFile = logoFile;
            return this;
        }

        /// <inheritdoc cref="MsiDefinition.OnLoad"/>
        public MsiBuilder OnLoad(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.Load, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.OnBeforeInstall"/>
        public MsiBuilder OnBeforeInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.BeforeInstall, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.OnAfterInstall"/>
        public MsiBuilder OnAfterInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            AddHandler(InstallStage.AfterInstall, handler, usesProperties);

        /// <inheritdoc cref="MsiDefinition.RunAfterInstall"/>
        public MsiBuilder RunAfterInstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
            AddCommand(new RunCommand(path, arguments, RunCommandTiming.AfterInstall), configure);

        /// <inheritdoc cref="MsiDefinition.RunBeforeUninstall"/>
        public MsiBuilder RunBeforeUninstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
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

        /// <summary>Installs a .NET Framework redistributable when it is missing.</summary>
        public BundleBuilder WithNetFramework(NetFrameworkVersion version)
        {
            ChainOf().NetFramework(version);
            return this;
        }

        /// <summary>Chains an MSI defined in the same plan; it is built first.</summary>
        public BundleBuilder WithMsi(MsiDefinition msi, Action<MsiChainItem>? configure = null)
        {
            ChainOf().Msi(msi, configure);
            return this;
        }

        /// <summary>Chains an existing MSI file.</summary>
        public BundleBuilder WithMsi(string msiPath, Action<MsiChainItem>? configure = null)
        {
            ChainOf().Msi(msiPath, configure);
            return this;
        }

        /// <summary>Chains an executable installer. Set a DetectCondition so it is skipped when present.</summary>
        public BundleBuilder WithExe(string exePath, Action<ExeChainItem>? configure = null)
        {
            ChainOf().Exe(exePath, configure);
            return this;
        }

        /// <summary>Adds an engine-specific customization. Prefer the engine package's extension methods.</summary>
        public BundleBuilder WithEngineCustomization(object customization)
        {
            MsiBuilder.ListOf(EngineCustomizations, WithEngineCustomizations).Add(customization);
            return this;
        }

        public BundleBuilder WithLogo(string logoFile)
        {
            var branding = Branding.Value;
            if (branding == null)
            {
                branding = new InstallKit.Branding();
                WithBranding(branding);
            }

            branding.LogoFile = logoFile;
            return this;
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
