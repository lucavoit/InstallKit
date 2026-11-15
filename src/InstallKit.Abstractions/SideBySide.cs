using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace InstallKit
{
    /// <summary>
    /// How versions of a product relate when they are installed on the same machine.
    /// With <see cref="None"/> a newer version replaces the older one (a major upgrade). With the other modes a version
    /// only replaces versions of its own <i>line</i>, and different lines are separate products that install side by side.
    /// </summary>
    public enum SideBySideMode
    {
        /// <summary>A newer version replaces the installed one. Default.</summary>
        None,

        /// <summary>One line per major version: 3.1.0 and 3.5.6 replace each other, 4.0.0 installs next to them.</summary>
        Major,

        /// <summary>One line per major and minor version: 3.5.2 replaces 3.5.1, but 3.1.0 and 3.5.x install side by side.</summary>
        MajorMinor,

        /// <summary>Every version is its own product and nothing is replaced.</summary>
        Full,
    }

    /// <summary>Turns a side-by-side setting into the names and codes that keep the lines apart.</summary>
    public static class SideBySide
    {
        /// <summary>The line a version belongs to for a mode, for example <c>3.1</c>. Empty for <see cref="SideBySideMode.None"/>.</summary>
        public static string LineOf(Version version, SideBySideMode mode)
        {
            switch (mode)
            {
                case SideBySideMode.Major:
                    return version.Major.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SideBySideMode.MajorMinor:
                    return $"{version.Major}.{version.Minor}";
                case SideBySideMode.Full:
                    return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// A stable GUID derived from a product's upgrade code and a line. The same inputs always give the same GUID, and
        /// different lines of one product give different ones, so each line is its own product for Windows Installer
        /// (and its components get their own GUIDs).
        /// </summary>
        public static Guid DeriveUpgradeCode(Guid baseCode, string line)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(baseCode.ToString("D").ToLowerInvariant() + "|installkit-side-by-side|" + line));
                var bytes = new byte[16];
                Array.Copy(hash, bytes, 16);
                bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // name-based (SHA-1) GUID
                bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
                return new Guid(bytes);
            }
        }

        /// <summary>
        /// Gives every MSI and bundle with a side-by-side mode the name, upgrade code and service names of its line.
        /// Runs once before validation; running it again changes nothing.
        /// </summary>
        public static void Apply(InstallerPlan plan)
        {
            foreach (var msi in plan.Msis)
            {
                if (msi.SideBySide == SideBySideMode.None || msi.AppliedSideBySideLine != null)
                    continue;

                var line = LineOf(msi.Version ?? plan.Context.Version, msi.SideBySide);
                msi.AppliedSideBySideLine = line;
                msi.UpgradeCode = Derive(msi.UpgradeCode, line);
                msi.Name = $"{msi.Name} {line}";

                foreach (var service in msi.Services)
                {
                    service.DisplayName = $"{service.DisplayName ?? service.Name} {line}";
                    service.Name = $"{service.Name}-{line}";
                }

                var shared = new List<string>();
                if (msi.RegistryEntries.Count > 0) shared.Add("registry values");
                if (msi.EnvironmentVariables.Count > 0) shared.Add("environment variables");
                if (msi.FileAssociations.Count > 0 || msi.UrlProtocols.Count > 0) shared.Add("file associations and URL protocols");
                if (msi.FirewallRules.Count > 0) shared.Add("firewall rules");
                if (shared.Count > 0)
                    Console.WriteLine($"InstallKit : warning IK273: '{msi.Name}' installs side by side, but {string.Join(", ", shared)} are machine-wide and not renamed for each version. Give them names that include the version if the versions must not share them.");
            }

            foreach (var bundle in plan.Bundles)
            {
                if (bundle.SideBySide == SideBySideMode.None || bundle.AppliedSideBySideLine != null)
                    continue;

                var line = LineOf(bundle.Version ?? plan.Context.Version, bundle.SideBySide);
                bundle.AppliedSideBySideLine = line;
                bundle.UpgradeCode = Derive(bundle.UpgradeCode, line);
                bundle.Name = $"{bundle.Name} {line}";
            }
        }

        // An upgrade code that is missing or not a GUID stays as it is, so the validator reports it as before.
        static string? Derive(string? upgradeCode, string line) =>
            Guid.TryParse(upgradeCode, out var guid) && guid != Guid.Empty ? DeriveUpgradeCode(guid, line).ToString("D") : upgradeCode;
    }
}
