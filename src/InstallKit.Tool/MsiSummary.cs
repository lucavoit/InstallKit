using System.Collections.Generic;

namespace InstallKit.Tool
{
    /// <summary>What an MSI installs, as plain data: the input of <c>inspect</c> and <c>diff</c>.</summary>
    public sealed class MsiSummary
    {
        public string? ProductName { get; set; }

        public string? Version { get; set; }

        public string? Manufacturer { get; set; }

        public string? UpgradeCode { get; set; }

        public string? ProductCode { get; set; }

        /// <summary>The platform from the summary information, for example <c>x64</c>.</summary>
        public string? Platform { get; set; }

        /// <summary>Whether the install is for all users (<c>ALLUSERS=1</c>).</summary>
        public bool PerMachine { get; set; }

        public List<string> Features { get; set; } = new List<string>();

        /// <summary>Installed files as <c>[Folder]\path (size)</c> entries; see <see cref="MsiFile"/>.</summary>
        public List<MsiFile> Files { get; set; } = new List<MsiFile>();

        public List<string> Services { get; set; } = new List<string>();

        public List<string> RegistryValues { get; set; } = new List<string>();

        public List<string> EnvironmentVariables { get; set; } = new List<string>();

        public List<string> Shortcuts { get; set; } = new List<string>();

        public List<string> CustomActions { get; set; } = new List<string>();

        public List<string> FirewallRules { get; set; } = new List<string>();
    }

    public sealed class MsiFile
    {
        /// <summary>Folder and name, for example <c>[INSTALLDIR]bin\App.exe</c>.</summary>
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }
    }
}
