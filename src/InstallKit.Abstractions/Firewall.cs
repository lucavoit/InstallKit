using System.Linq;

namespace InstallKit
{
    /// <summary>The protocol a firewall rule applies to.</summary>
    public enum FirewallProtocol
    {
        Tcp,
        Udp,

        /// <summary>Any protocol. Only valid for a rule that names a program and no port.</summary>
        Any,
    }

    /// <summary>Which remote addresses a firewall rule lets in.</summary>
    public enum FirewallScope
    {
        /// <summary>Any address.</summary>
        Any,

        /// <summary>Only the local subnet.</summary>
        LocalSubnet,
    }

    /// <summary>The Windows Firewall profiles a rule is active in.</summary>
    public enum FirewallProfile
    {
        All,
        Domain,
        Private,
        Public,
    }

    /// <summary>
    /// An inbound Windows Firewall exception created at install time and removed on uninstall.
    /// A rule names a port, a program of the product, or both. It needs an elevated (per-machine) install.
    /// </summary>
    public sealed class FirewallRule
    {
        public FirewallRule(string name, string? port = null, FirewallProtocol protocol = FirewallProtocol.Tcp, string? executable = null,
            FirewallScope scope = FirewallScope.Any, FirewallProfile profile = FirewallProfile.All)
        {
            Name = name;
            Port = string.IsNullOrWhiteSpace(port) ? null : port!.Trim();
            Protocol = protocol;
            Executable = string.IsNullOrWhiteSpace(executable) ? null : executable;
            Scope = scope;
            Profile = profile;
        }

        /// <summary>The rule name shown in Windows Defender Firewall.</summary>
        public string Name { get; }

        /// <summary>A port (<c>8080</c>), a range (<c>8000-8100</c>) or a comma separated list (<c>80,443</c>), or <c>null</c> to allow every port of the program.</summary>
        public string? Port { get; }

        public FirewallProtocol Protocol { get; }

        /// <summary>A program of the product, relative to the install directory, or <c>null</c> to allow the port for every program.</summary>
        public string? Executable { get; }

        public FirewallScope Scope { get; }

        public FirewallProfile Profile { get; }

        /// <summary>Whether <see cref="Port"/> is empty or a list of ports and ranges between 1 and 65535.</summary>
        internal bool HasValidPort
        {
            get
            {
                if (Port == null)
                    return true;

                return Port.Split(',').All(part =>
                {
                    var bounds = part.Trim().Split('-');
                    return bounds.Length <= 2 && bounds.All(IsPortNumber) &&
                           (bounds.Length == 1 || int.Parse(bounds[0]) <= int.Parse(bounds[1]));
                });
            }
        }

        static bool IsPortNumber(string text) =>
            text.Length > 0 && text.Length <= 5 && text.All(c => c >= '0' && c <= '9') && int.Parse(text) >= 1 && int.Parse(text) <= 65535;
    }
}
