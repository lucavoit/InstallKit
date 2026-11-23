using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace InstallKit
{
    /// <summary>Warnings about an MSI whose package platform does not match the files it installs (IK122).</summary>
    public static class PlatformChecks
    {
        static readonly Regex RuntimeTarget = new Regex(@"""runtimeTarget""\s*:\s*\{\s*""name""\s*:\s*""[^""]*/(win-(?:x64|x86|arm64))""", RegexOptions.CultureInvariant);

        /// <summary>The runtime identifier (<c>win-x64</c>, ...) a published folder was built for, or <c>null</c> when it is not known.</summary>
        public static string? DetectRuntimeIdentifier(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return null;

                foreach (var file in Directory.GetFiles(directory, "*.deps.json", SearchOption.TopDirectoryOnly))
                {
                    var match = RuntimeTarget.Match(File.ReadAllText(file));
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return null;
        }

        /// <summary>The runtime identifier that fits a package platform.</summary>
        public static string RuntimeIdentifierOf(InstallerPlatform platform) => platform switch
        {
            InstallerPlatform.X86 => "win-x86",
            InstallerPlatform.Arm64 => "win-arm64",
            _ => "win-x64",
        };

        /// <summary>Compares the platform of every MSI with the runtime identifier of its payload folders.</summary>
        public static IReadOnlyList<string> Warnings(InstallerPlan plan)
        {
            var warnings = new List<string>();

            foreach (var msi in plan.Msis)
            {
                var expected = RuntimeIdentifierOf(msi.Platform);
                foreach (var payload in msi.Payloads)
                {
                    var directory = InstallerValidator.ResolvePayload(payload, plan.Context);
                    var found = directory == null ? null : DetectRuntimeIdentifier(directory);
                    if (found == null || string.Equals(found, expected, StringComparison.Ordinal))
                        continue;

                    var name = payload.PayloadName ?? payload.Directory;
                    warnings.Add($"InstallKit : warning IK122: MSI '{msi.Name}' is a {msi.Platform} package ({DescribeConsequences(msi.Platform)}) " +
                        $"but the files of '{name}' were published for {found}. Set .WithPlatform(InstallerPlatform.{PlatformOf(found)}) on the MSI, or publish the payload for {expected}.");
                }
            }

            return warnings;
        }

        static string PlatformOf(string runtimeIdentifier) => runtimeIdentifier switch
        {
            "win-x86" => "X86",
            "win-arm64" => "Arm64",
            _ => "X64",
        };

        static string DescribeConsequences(InstallerPlatform platform) => platform == InstallerPlatform.X86
            ? "installs below 'Program Files (x86)', writes the 32-bit registry view and runs handlers as 32-bit code"
            : "installs below 'Program Files' with the 64-bit registry view";
    }
}
