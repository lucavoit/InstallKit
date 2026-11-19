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

        /// <summary>
        /// Whether versions of the bundle install side by side instead of replacing each other. Use the same mode as the
        /// MSIs it chains, otherwise a newer bundle replaces the older one together with its MSIs.
        /// </summary>
        public SideBySideMode SideBySide { get; set; } = SideBySideMode.None;

        // Set when the side-by-side naming was applied; a field so the generated builder does not expose it.
        internal string? AppliedSideBySideLine;

        /// <summary>Output file name without extension. Defaults to the name without spaces plus <c>Setup</c>.</summary>
        public string? OutputFileName { get; set; }

        /// <summary>An RTF license file, or a URL shown as a license link. Optional.</summary>
        public string? License { get; set; }

        public string? IconFile { get; set; }

        /// <summary>Logo, theme and colours of the bundle window. The dialog images (banner, background) do not apply to bundles.</summary>
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
        readonly List<BundleSearch> searches = new List<BundleSearch>();

        public IReadOnlyList<ChainItem> Items => items;

        /// <summary>Lookups the setup makes before installing; added by the prerequisite helpers.</summary>
        public IReadOnlyList<BundleSearch> Searches => searches;

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

        /// <summary>
        /// Installs the Visual C++ Redistributable (2015 to 2022) unless it is already present. The installer is the one
        /// you download from Microsoft (<c>vc_redist.x64.exe</c>), embedded or downloaded when the setup runs.
        /// </summary>
        public BundleChain VcRedist(PrerequisiteInstaller installer, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64)
        {
            var variable = "InstallKitVcRedist" + architecture;
            var folder = architecture == PrerequisiteArchitecture.X86 ? "x86" : architecture == PrerequisiteArchitecture.Arm64 ? "arm64" : "x64";
            // The x86 runtime registers itself in the 32-bit view of the registry, the others in the 64-bit view.
            searches.Add(new RegistryBundleSearch(variable, RegistryHive.LocalMachine, $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{folder}", "Installed", architecture != PrerequisiteArchitecture.X86));
            return AddPrerequisite(installer, "/install /quiet /norestart", $"{variable} = 1");
        }

        /// <summary>Installs a .NET runtime of the given major version unless a runtime with that major version (or a newer patch) is present.</summary>
        public BundleChain DotNetRuntime(DotNetRuntimeKind kind, int majorVersion, PrerequisiteInstaller installer, PrerequisiteArchitecture architecture = PrerequisiteArchitecture.X64)
        {
            var variable = $"InstallKitDotNet{kind}{majorVersion}{architecture}";
            searches.Add(new DotNetBundleSearch(variable, kind, majorVersion, architecture));
            return AddPrerequisite(installer, "/install /quiet /norestart", $"{variable} >= v{majorVersion}.0.0.0");
        }

        /// <summary>
        /// Installs the Microsoft Edge WebView2 Runtime unless it is already present for this machine or this user.
        /// The installer is Microsoft's Evergreen Bootstrapper or Standalone Installer.
        /// </summary>
        public BundleChain WebView2(PrerequisiteInstaller installer)
        {
            const string key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
            searches.Add(new RegistryBundleSearch("InstallKitWebView2", RegistryHive.LocalMachine, key, "pv", win64: false));
            searches.Add(new RegistryBundleSearch("InstallKitWebView2User", RegistryHive.CurrentUser, key, "pv", win64: false));
            return AddPrerequisite(installer, "/silent /install",
                "(InstallKitWebView2 <> \"\" AND InstallKitWebView2 <> \"0.0.0.0\") OR (InstallKitWebView2User <> \"\" AND InstallKitWebView2User <> \"0.0.0.0\")");
        }

        BundleChain AddPrerequisite(PrerequisiteInstaller installer, string arguments, string detectCondition)
        {
            var item = installer.IsDownload ? new ExeChainItem(installer.ResolvedFileName, installer) : new ExeChainItem(installer.FilePath!, null);
            item.InstallArguments = arguments;
            item.DetectCondition = detectCondition;
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
        internal ExeChainItem(string path, PrerequisiteInstaller? download = null)
        {
            Path = path;
            Download = download;
        }

        /// <summary>The installer file, or for a download the name it is saved as.</summary>
        public string Path { get; }

        /// <summary>Set when the installer is downloaded when the setup runs instead of being embedded.</summary>
        public PrerequisiteInstaller? Download { get; }

        public string? InstallArguments { get; set; } = "/quiet /norestart";

        public string? UninstallArguments { get; set; }

        /// <summary>Burn condition that is true when the package is already installed.</summary>
        public string? DetectCondition { get; set; }

        public bool PerMachine { get; set; } = true;

        /// <summary>Keep the package installed when the bundle is uninstalled (typical for runtimes).</summary>
        public bool Permanent { get; set; } = true;
    }
}
