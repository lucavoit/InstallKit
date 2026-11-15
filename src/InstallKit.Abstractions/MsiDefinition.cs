using System;
using System.Collections.Generic;

namespace InstallKit
{
    /// <summary>
    /// Describes one MSI package. Every property has a sensible default except <see cref="UpgradeCode"/>,
    /// which must stay stable for the lifetime of the product so upgrades work.
    /// </summary>
    public sealed class MsiDefinition
    {
        internal MsiDefinition(string name) => Name = name;

        // Used by the generated MsiBuilder.
        internal MsiDefinition() : this(string.Empty) { }

        /// <summary>Product name shown in Add/Remove Programs.</summary>
        public string Name { get; internal set; }

        /// <summary>Company name. Used in the default install directory and shortcuts.</summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// The product's upgrade code (a GUID). Generate it once and never change it:
        /// a new value makes Windows treat the next version as a different product.
        /// </summary>
        public string? UpgradeCode { get; set; }

        /// <summary>Overrides the version passed in by the build (<c>-p:Version=...</c>).</summary>
        public Version? Version { get; set; }

        public string? Description { get; set; }

        /// <summary>Install directory. Defaults to <c>%ProgramFiles%\Manufacturer\Name</c>.</summary>
        public string? InstallDirectory { get; set; }

        public InstallerPlatform Platform { get; set; } = InstallerPlatform.X64;

        public InstallScope Scope { get; set; } = InstallScope.PerMachine;

        public InstallerUi Ui { get; set; } = InstallerUi.Minimal;

        /// <summary>Optional RTF license shown by the UI.</summary>
        public string? LicenseFile { get; set; }

        /// <summary>Optional .ico used in Add/Remove Programs.</summary>
        public string? IconFile { get; set; }

        /// <summary>Output file name without extension. Defaults to the name without spaces.</summary>
        public string? OutputFileName { get; set; }

        /// <summary>
        /// The main executable, relative to the install directory (for example <c>MyApp.exe</c>).
        /// Shortcuts point to it.
        /// </summary>
        public string? MainExecutable { get; set; }

        public ShortcutOptions Shortcuts { get; set; } = new ShortcutOptions();

        /// <summary>Accent colour, logo and dialog images.</summary>
        public Branding Branding { get; set; } = new Branding();

        /// <summary>Code that runs on the target machine during the install. See <see cref="OnAfterInstall"/>.</summary>
        public IList<InstallEventHandler> EventHandlers { get; set; } = new List<InstallEventHandler>();

        /// <summary>Programs run during the install. See <see cref="RunAfterInstall"/>.</summary>
        public IList<RunCommand> Commands { get; set; } = new List<RunCommand>();

        public IList<PayloadSource> Payloads { get; set; } = new List<PayloadSource>();

        public IList<ServiceDefinition> Services { get; set; } = new List<ServiceDefinition>();

        /// <summary>Environment variable and <c>PATH</c> changes made at install time and undone on uninstall.</summary>
        public IList<EnvironmentVariableEntry> EnvironmentVariables { get; set; } = new List<EnvironmentVariableEntry>();

        /// <summary>File extensions that open with this product. See <see cref="AddFileAssociation"/>.</summary>
        public IList<FileAssociation> FileAssociations { get; set; } = new List<FileAssociation>();

        /// <summary>URL schemes handled by this product. See <see cref="AddUrlProtocol"/>.</summary>
        public IList<UrlProtocol> UrlProtocols { get; set; } = new List<UrlProtocol>();

        /// <summary>Inbound Windows Firewall exceptions. See <see cref="AddFirewallRule"/>.</summary>
        public IList<FirewallRule> FirewallRules { get; set; } = new List<FirewallRule>();

        /// <summary>Values set in installed XML files. See <see cref="AddXmlValue"/>.</summary>
        public IList<XmlValue> XmlValues { get; set; } = new List<XmlValue>();

        /// <summary>Programs that are closed (and optionally ended) while installing. See <see cref="AddCloseApplication"/>.</summary>
        public IList<CloseApplicationRule> CloseApplications { get; set; } = new List<CloseApplicationRule>();

        /// <summary>Optional parts of the product. See <see cref="AddFeature"/>.</summary>
        public IList<OptionalFeature> Features { get; set; } = new List<OptionalFeature>();

        /// <summary>Access granted to accounts on folders of the product. See <see cref="AddFolderPermission"/>.</summary>
        public IList<FolderPermission> FolderPermissions { get; set; } = new List<FolderPermission>();

        /// <summary>Registry values written at install time and removed on uninstall. See <see cref="AddRegistryValue(RegistryHive, string, string?, string, RegistryValueKind)"/>.</summary>
        public IList<RegistryEntry> RegistryEntries { get; set; } = new List<RegistryEntry>();

        /// <summary>Public MSI properties (for example values a bundle passes in).</summary>
        public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Engine-specific customizations. Use the engine package's extension methods
        /// (for example <c>ConfigureWixSharp</c>) rather than adding to this directly.
        /// </summary>
        public IList<object> EngineCustomizations { get; set; } = new List<object>();

        /// <summary>
        /// Includes the published output of an <c>&lt;InstallerPayload&gt;</c> project.
        /// </summary>
        /// <param name="payloadName">The payload name: the project file name without extension, or its <c>Name</c> metadata.</param>
        /// <param name="targetSubdirectory">Optional folder under the install directory.</param>
        /// <param name="feature">Optional <see cref="OptionalFeature"/> these files belong to.</param>
        public MsiDefinition AddPayload(string payloadName, string? targetSubdirectory = null, string? feature = null)
        {
            Payloads.Add(PayloadSource.FromPayload(payloadName, targetSubdirectory, feature));
            return this;
        }

        /// <summary>Includes every file of a directory (recursively).</summary>
        public MsiDefinition AddDirectory(string sourceDirectory, string? targetSubdirectory = null, string? feature = null)
        {
            Payloads.Add(PayloadSource.FromDirectory(sourceDirectory, targetSubdirectory, feature));
            return this;
        }

        /// <summary>Runs when the install session starts, before anything is changed. Not elevated.</summary>
        /// <param name="handler">A static method passed as a method group; it runs on the target machine.</param>
        /// <param name="usesProperties">MSI properties the handler reads.</param>
        public MsiDefinition OnLoad(Action<InstallEvent> handler, params string[] usesProperties) =>
            On(InstallStage.Load, handler, usesProperties);

        /// <summary>Runs before files are copied. Not elevated.</summary>
        /// <param name="handler">A static method passed as a method group; it runs on the target machine.</param>
        /// <param name="usesProperties">MSI properties the handler reads.</param>
        public MsiDefinition OnBeforeInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            On(InstallStage.BeforeInstall, handler, usesProperties);

        /// <summary>
        /// Runs after files are copied, elevated. Also runs on uninstall and repair: check
        /// <see cref="InstallEvent.IsInstalling"/> and friends.
        /// </summary>
        /// <param name="handler">A static method passed as a method group; it runs on the target machine.</param>
        /// <param name="usesProperties">MSI properties the handler reads; only these are available at this stage.</param>
        public MsiDefinition OnAfterInstall(Action<InstallEvent> handler, params string[] usesProperties) =>
            On(InstallStage.AfterInstall, handler, usesProperties);

        /// <summary>Runs a program after files are installed (fresh install or upgrade).</summary>
        /// <param name="path">Relative to the install directory, or an MSI formatted path.</param>
        /// <param name="arguments">Command-line arguments. MSI properties like [INSTALLDIR] are expanded.</param>
        /// <param name="configure">Elevation and exit code handling.</param>
        public MsiDefinition RunAfterInstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
            Run(new RunCommand(path, arguments, RunCommandTiming.AfterInstall), configure);

        /// <summary>Runs a program before files are removed on uninstall (not on upgrade).</summary>
        /// <param name="path">Relative to the install directory, or an MSI formatted path.</param>
        /// <param name="arguments">Command-line arguments. MSI properties like [INSTALLDIR] are expanded.</param>
        /// <param name="configure">Elevation and exit code handling.</param>
        public MsiDefinition RunBeforeUninstall(string path, string? arguments = null, Action<RunCommand>? configure = null) =>
            Run(new RunCommand(path, arguments, RunCommandTiming.BeforeUninstall), configure);

        MsiDefinition On(InstallStage stage, Action<InstallEvent> handler, string[] usesProperties)
        {
            EventHandlers.Add(new InstallEventHandler(stage, handler, usesProperties));
            return this;
        }

        MsiDefinition Run(RunCommand command, Action<RunCommand>? configure)
        {
            configure?.Invoke(command);
            Commands.Add(command);
            return this;
        }

        /// <summary>Installs the executable as a Windows service.</summary>
        /// <param name="executable">Path relative to the install directory.</param>
        /// <param name="serviceName">The service name.</param>
        /// <param name="configure">Optional extra settings.</param>
        public MsiDefinition AddService(string executable, string serviceName, Action<ServiceDefinition>? configure = null)
        {
            var service = new ServiceDefinition(executable, serviceName);
            configure?.Invoke(service);
            Services.Add(service);
            return this;
        }

        /// <summary>Writes a registry value on install and removes it on uninstall.</summary>
        /// <param name="hive">The registry root.</param>
        /// <param name="key">The key below the root, for example <c>Software\Contoso\My App</c>.</param>
        /// <param name="name">The value name, or <c>null</c> for the key's default value.</param>
        /// <param name="value">The value. MSI formatted text such as <c>[INSTALLDIR]</c> is expanded.</param>
        /// <param name="kind">The value type.</param>
        public MsiDefinition AddRegistryValue(RegistryHive hive, string key, string? name, string value, RegistryValueKind kind = RegistryValueKind.String)
        {
            RegistryEntries.Add(new RegistryEntry(hive, key, name, value, kind));
            return this;
        }

        /// <summary>Writes a <c>REG_DWORD</c> registry value on install and removes it on uninstall.</summary>
        public MsiDefinition AddRegistryValue(RegistryHive hive, string key, string? name, int value) =>
            AddRegistryValue(hive, key, name, value.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.DWord);

        /// <summary>Sets, appends to or prepends to an environment variable. The change is undone on uninstall.</summary>
        public MsiDefinition AddEnvironmentVariable(string name, string value, EnvironmentVariableAction action = EnvironmentVariableAction.Set)
        {
            EnvironmentVariables.Add(new EnvironmentVariableEntry(name, value, action));
            return this;
        }

        /// <summary>Adds a folder to <c>PATH</c> and removes it again on uninstall.</summary>
        /// <param name="path">The folder, for example <c>[INSTALLDIR]bin</c>.</param>
        /// <param name="prepend">Put the folder at the start of <c>PATH</c> instead of the end.</param>
        public MsiDefinition AddPathEntry(string path, bool prepend = false) =>
            AddEnvironmentVariable("PATH", path, prepend ? EnvironmentVariableAction.Prepend : EnvironmentVariableAction.Append);

        /// <summary>Makes a file extension open with one of the product's programs.</summary>
        /// <param name="extension">The extension with its dot, for example <c>.foo</c>.</param>
        /// <param name="description">The file type name Explorer shows.</param>
        /// <param name="executable">The program, relative to the install directory. Defaults to <see cref="MainExecutable"/>.</param>
        /// <param name="progId">The ProgId. Defaults to the output file name plus the extension.</param>
        public MsiDefinition AddFileAssociation(string extension, string description, string? executable = null, string? progId = null)
        {
            FileAssociations.Add(new FileAssociation(extension, description, executable, progId));
            return this;
        }

        /// <summary>Registers a URL scheme such as <c>myapp://</c> that starts one of the product's programs.</summary>
        /// <param name="scheme">The scheme without <c>://</c>.</param>
        /// <param name="description">Shown to the user when the browser asks which program should open the link.</param>
        /// <param name="executable">The program, relative to the install directory. Defaults to <see cref="MainExecutable"/>.</param>
        public MsiDefinition AddUrlProtocol(string scheme, string description, string? executable = null)
        {
            UrlProtocols.Add(new UrlProtocol(scheme, description, executable));
            return this;
        }

        /// <summary>
        /// Everything written to the registry: the values added directly plus those that file associations and
        /// URL protocols are made of. Engines write this list.
        /// </summary>
        public IReadOnlyList<RegistryEntry> ResolvedRegistryEntries
        {
            get
            {
                var all = new List<RegistryEntry>(RegistryEntries);
                foreach (var association in FileAssociations)
                    all.AddRange(association.ToRegistryEntries(this));
                foreach (var protocol in UrlProtocols)
                    all.AddRange(protocol.ToRegistryEntries(this));
                return all;
            }
        }

        /// <summary>Opens a port, a program or both in Windows Firewall (inbound) and removes the rule on uninstall.</summary>
        /// <param name="name">The rule name shown in Windows Defender Firewall.</param>
        /// <param name="port">A port (<c>8080</c>), a range (<c>8000-8100</c>) or a list (<c>80,443</c>), or <c>null</c> for every port of the program.</param>
        /// <param name="protocol">The protocol. Use <see cref="FirewallProtocol.Any"/> for a program without a port.</param>
        /// <param name="executable">A program of the product relative to the install directory, or <c>null</c> to allow the port for every program.</param>
        /// <param name="scope">Which remote addresses are allowed.</param>
        /// <param name="profile">The firewall profiles the rule is active in.</param>
        public MsiDefinition AddFirewallRule(string name, string? port = null, FirewallProtocol protocol = FirewallProtocol.Tcp, string? executable = null,
            FirewallScope scope = FirewallScope.Any, FirewallProfile profile = FirewallProfile.All)
        {
            FirewallRules.Add(new FirewallRule(name, port, protocol, executable, scope, profile));
            return this;
        }

        /// <summary>Sets an attribute or element value in an installed XML file during the install.</summary>
        /// <param name="file">The file relative to the install directory, for example <c>MyApp.exe.config</c>.</param>
        /// <param name="xpath">An XPath to one element or attribute, for example <c>/configuration/appSettings/add[@key='Server']/@value</c>.</param>
        /// <param name="value">The new value. MSI formatted text such as <c>[SERVER]</c> is expanded.</param>
        public MsiDefinition AddXmlValue(string file, string xpath, string value)
        {
            XmlValues.Add(new XmlValue(file, xpath, value));
            return this;
        }

        /// <summary>
        /// Closes a running program before files are replaced or removed, so they are not locked and no restart is needed.
        /// It is asked to close first; with <paramref name="terminate"/> it is ended when it does not.
        /// </summary>
        /// <param name="processName">The process file name, for example <c>MyApp.exe</c>.</param>
        /// <param name="terminate">End the process if it is still running after <paramref name="timeoutSeconds"/>.</param>
        /// <param name="timeoutSeconds">How long to wait for the program to close.</param>
        public MsiDefinition AddCloseApplication(string processName, bool terminate = false, int timeoutSeconds = 5)
        {
            CloseApplications.Add(new CloseApplicationRule(processName, terminate, timeoutSeconds));
            return this;
        }

        /// <summary>
        /// Declares an optional part of the product. Assign files to it with the <c>feature</c> argument of
        /// <see cref="AddPayload"/> and <see cref="AddDirectory"/>. Users choose features in the
        /// <see cref="InstallerUi.FeatureTree"/> dialogs or with <c>ADDLOCAL</c> and <c>REMOVE</c> on the command line.
        /// </summary>
        /// <param name="name">The name users see. Also used on the command line, with spaces replaced by underscores.</param>
        /// <param name="description">Shown next to the feature in the feature tree.</param>
        /// <param name="installedByDefault">Whether the feature is installed when the user does not choose.</param>
        public MsiDefinition AddFeature(string name, string? description = null, bool installedByDefault = true)
        {
            Features.Add(new OptionalFeature(name, description, installedByDefault));
            return this;
        }

        /// <summary>
        /// Gives an account access to the install directory or one of its subfolders, for example so a service that runs
        /// as a limited account can write to its data folder. A folder that holds no files of the product is created.
        /// </summary>
        /// <param name="account">For example <c>Users</c>, <c>Authenticated Users</c>, <c>NT AUTHORITY\LocalService</c> or <c>NT SERVICE\MyService</c>.</param>
        /// <param name="rights">How much access the account gets.</param>
        /// <param name="folder">A folder below the install directory such as <c>data</c> or <c>data\logs</c>, or <c>null</c> for the install directory itself.</param>
        public MsiDefinition AddFolderPermission(string account, FolderRights rights, string? folder = null)
        {
            FolderPermissions.Add(new FolderPermission(account, rights, folder));
            return this;
        }

        /// <summary>Output file name with defaults applied.</summary>
        public string ResolvedOutputFileName => string.IsNullOrWhiteSpace(OutputFileName) ? Name.Replace(" ", string.Empty) : OutputFileName!;

        /// <summary>Install directory with defaults applied.</summary>
        public string ResolvedInstallDirectory =>
            string.IsNullOrWhiteSpace(InstallDirectory)
                ? (string.IsNullOrWhiteSpace(Manufacturer) ? $@"%ProgramFiles%\{Name}" : $@"%ProgramFiles%\{Manufacturer}\{Name}")
                : InstallDirectory!;
    }

    /// <summary>Which shortcuts to create for <see cref="MsiDefinition.MainExecutable"/>.</summary>
    public sealed class ShortcutOptions
    {
        public bool StartMenu { get; set; } = true;

        public bool Desktop { get; set; }
    }

    /// <summary>Where a set of installed files comes from.</summary>
    public sealed class PayloadSource
    {
        PayloadSource(string? payloadName, string? directory, string? targetSubdirectory, string? feature)
        {
            Feature = string.IsNullOrWhiteSpace(feature) ? null : feature;
            PayloadName = payloadName;
            Directory = directory;
            TargetSubdirectory = string.IsNullOrWhiteSpace(targetSubdirectory) ? null : targetSubdirectory!.Trim('\\', '/');
        }

        /// <summary>Name of an MSBuild <c>&lt;InstallerPayload&gt;</c> item, or <c>null</c>.</summary>
        public string? PayloadName { get; }

        /// <summary>Explicit source directory, or <c>null</c>.</summary>
        public string? Directory { get; }

        /// <summary>Folder under the install directory, or <c>null</c> for the root.</summary>
        public string? TargetSubdirectory { get; }

        /// <summary>The <see cref="OptionalFeature"/> these files belong to, or <c>null</c> for files that are always installed.</summary>
        public string? Feature { get; }

        public static PayloadSource FromPayload(string payloadName, string? targetSubdirectory = null, string? feature = null) =>
            new PayloadSource(payloadName, null, targetSubdirectory, feature);

        public static PayloadSource FromDirectory(string directory, string? targetSubdirectory = null, string? feature = null) =>
            new PayloadSource(null, directory, targetSubdirectory, feature);
    }

    /// <summary>A Windows service installed with the MSI.</summary>
    public sealed class ServiceDefinition
    {
        internal ServiceDefinition(string executable, string name)
        {
            Executable = executable;
            Name = name;
        }

        // Used by the generated ServiceBuilder.
        internal ServiceDefinition() : this(string.Empty, string.Empty) { }

        /// <summary>Path of the service executable relative to the install directory.</summary>
        public string Executable { get; internal set; }

        public string Name { get; internal set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public ServiceStartMode StartMode { get; set; } = ServiceStartMode.Automatic;

        /// <summary>Account to run as. Defaults to LocalSystem.</summary>
        public string? Account { get; set; }

        public string? Arguments { get; set; }

        /// <summary>Start the service at the end of the install. Default <c>true</c>.</summary>
        public bool StartOnInstall { get; set; } = true;
    }
}
