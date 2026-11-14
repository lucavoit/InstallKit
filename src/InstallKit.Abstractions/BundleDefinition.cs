using System;
using System.Collections.Generic;

namespace InstallKit
{
    /// <summary>
    /// Describes a bundle: a setup .exe that installs prerequisites and one or more MSIs in order.
    /// </summary>
    public sealed class BundleDefinition
    {
        internal BundleDefinition(string name) => Name = name;

        // Used by the generated BundleBuilder.
        internal BundleDefinition() : this(string.Empty) { }

        public string Name { get; internal set; }

        public string? Manufacturer { get; set; }

        /// <summary>The bundle's own upgrade code. Must differ from every MSI's upgrade code.</summary>
        public string? UpgradeCode { get; set; }

        public Version? Version { get; set; }

        /// <summary>Output file name without extension. Defaults to the name without spaces plus <c>Setup</c>.</summary>
        public string? OutputFileName { get; set; }

        /// <summary>An RTF license file, or a URL shown as a license link. Optional.</summary>
        public string? License { get; set; }

        public string? IconFile { get; set; }

        /// <summary>Logo shown by the bundle UI. Accent colours and dialog images do not apply to bundles.</summary>
        public Branding Branding { get; set; } = new Branding();

        /// <summary>The packages installed, in order.</summary>
        public BundleChain Chain { get; set; } = new BundleChain();

        /// <summary>Engine-specific customizations. See <see cref="MsiDefinition.EngineCustomizations"/>.</summary>
        public IList<object> EngineCustomizations { get; set; } = new List<object>();

        /// <summary>Output file name with defaults applied.</summary>
        public string ResolvedOutputFileName =>
            string.IsNullOrWhiteSpace(OutputFileName) ? Name.Replace(" ", string.Empty) + "Setup" : OutputFileName!;
    }

    /// <summary>The ordered list of packages in a bundle.</summary>
    public sealed class BundleChain
    {
        readonly List<ChainItem> items = new List<ChainItem>();

        public IReadOnlyList<ChainItem> Items => items;

        /// <summary>Installs a .NET Framework redistributable when it is missing (downloaded on demand).</summary>
        public BundleChain NetFramework(NetFrameworkVersion version)
        {
            items.Add(new NetFrameworkChainItem(version));
            return this;
        }

        /// <summary>Chains an MSI defined in the same installer project. It is built first.</summary>
        public BundleChain Msi(MsiDefinition msi, Action<MsiChainItem>? configure = null)
        {
            var item = new MsiChainItem(msi, null);
            configure?.Invoke(item);
            items.Add(item);
            return this;
        }

        /// <summary>Chains an existing MSI file.</summary>
        public BundleChain Msi(string msiPath, Action<MsiChainItem>? configure = null)
        {
            var item = new MsiChainItem(null, msiPath);
            configure?.Invoke(item);
            items.Add(item);
            return this;
        }

        /// <summary>Chains an executable installer, such as a .NET runtime or a VC++ redistributable.</summary>
        /// <param name="exePath">Path to the installer, embedded into the bundle.</param>
        /// <param name="configure">Arguments and detection. Set <see cref="ExeChainItem.DetectCondition"/> so it is skipped when already present.</param>
        public BundleChain Exe(string exePath, Action<ExeChainItem>? configure = null)
        {
            var item = new ExeChainItem(exePath);
            configure?.Invoke(item);
            items.Add(item);
            return this;
        }
    }

    public abstract class ChainItem
    {
        /// <summary>Abort the bundle when this package fails. Default <c>true</c>.</summary>
        public bool Vital { get; set; } = true;

        /// <summary>Optional Burn condition; the package is skipped when it evaluates to false.</summary>
        public string? InstallCondition { get; set; }
    }

    public sealed class NetFrameworkChainItem : ChainItem
    {
        internal NetFrameworkChainItem(NetFrameworkVersion version) => Version = version;

        public NetFrameworkVersion Version { get; }
    }

    public sealed class MsiChainItem : ChainItem
    {
        internal MsiChainItem(MsiDefinition? definition, string? path)
        {
            Definition = definition;
            Path = path;
        }

        /// <summary>The MSI defined in this project, or <c>null</c> when <see cref="Path"/> is used.</summary>
        public MsiDefinition? Definition { get; }

        public string? Path { get; }

        /// <summary>Show the MSI's own dialogs instead of only the bundle UI.</summary>
        public bool DisplayInternalUi { get; set; }

        /// <summary>MSI properties passed to the package.</summary>
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
    }

    public sealed class ExeChainItem : ChainItem
    {
        internal ExeChainItem(string path) => Path = path;

        public string Path { get; }

        public string? InstallArguments { get; set; } = "/quiet /norestart";

        public string? UninstallArguments { get; set; }

        /// <summary>Burn condition that is true when the package is already installed.</summary>
        public string? DetectCondition { get; set; }

        public bool PerMachine { get; set; } = true;

        /// <summary>Keep the package installed when the bundle is uninstalled (typical for runtimes).</summary>
        public bool Permanent { get; set; } = true;
    }
}
