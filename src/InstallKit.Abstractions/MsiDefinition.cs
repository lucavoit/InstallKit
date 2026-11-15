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
        public MsiDefinition AddPayload(string payloadName, string? targetSubdirectory = null)
        {
            Payloads.Add(PayloadSource.FromPayload(payloadName, targetSubdirectory));
            return this;
        }

        /// <summary>Includes every file of a directory (recursively).</summary>
        public MsiDefinition AddDirectory(string sourceDirectory, string? targetSubdirectory = null)
        {
            Payloads.Add(PayloadSource.FromDirectory(sourceDirectory, targetSubdirectory));
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
        PayloadSource(string? payloadName, string? directory, string? targetSubdirectory)
        {
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

        public static PayloadSource FromPayload(string payloadName, string? targetSubdirectory = null) =>
            new PayloadSource(payloadName, null, targetSubdirectory);

        public static PayloadSource FromDirectory(string directory, string? targetSubdirectory = null) =>
            new PayloadSource(null, directory, targetSubdirectory);
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
