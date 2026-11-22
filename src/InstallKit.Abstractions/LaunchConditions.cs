using System;

namespace InstallKit
{
    /// <summary>
    /// A condition the target machine must meet for the setup to run (a WiX <c>Launch</c> condition).
    /// When the condition is false the setup shows <see cref="Message"/> and stops.
    /// </summary>
    public sealed class LaunchConditionEntry
    {
        public LaunchConditionEntry(string condition, string message)
        {
            Condition = condition;
            Message = message;
        }

        /// <summary>An MSI condition, for example <c>VersionNT64</c> or <c>WindowsBuild &gt;= 19041</c>. The setup runs when it is true.</summary>
        public string Condition { get; }

        /// <summary>What the user sees when the condition is false.</summary>
        public string Message { get; }

        public override string ToString() => $"{Condition} ({Message})";
    }

    /// <summary>
    /// Reads a registry value into an MSI property when the setup starts, so a launch condition can test it.
    /// Added by the <see cref="RequirementsBuilder"/> helpers.
    /// </summary>
    public sealed class RegistryPropertySearch
    {
        public RegistryPropertySearch(string property, RegistryHive hive, string key, string valueName, bool win64)
        {
            Property = property;
            Hive = hive;
            Key = key;
            ValueName = valueName;
            Win64 = win64;
        }

        /// <summary>The MSI property that receives the value (empty when the value does not exist).</summary>
        public string Property { get; }

        public RegistryHive Hive { get; }

        public string Key { get; }

        public string ValueName { get; }

        /// <summary>Read the 64-bit registry view (otherwise the 32-bit view).</summary>
        public bool Win64 { get; }
    }

    /// <summary>The registry searches and conditions behind the .NET Framework and WebView2 requirements.</summary>
    public static class RequirementConditions
    {
        /// <summary>The property that holds the .NET Framework 4.x <c>Release</c> number.</summary>
        public const string NetFrameworkReleaseProperty = "INSTALLKIT_NETFX_RELEASE";

        public const string WebView2MachineProperty = "INSTALLKIT_WEBVIEW2";

        public const string WebView2UserProperty = "INSTALLKIT_WEBVIEW2_USER";

        const string NetFrameworkKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";

        const string WebView2Key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        /// <summary>The <c>Release</c> value of the first build of each .NET Framework version.</summary>
        public static int NetFrameworkRelease(NetFrameworkVersion version) => version switch
        {
            NetFrameworkVersion.NetFx462 => 394802,
            NetFrameworkVersion.NetFx472 => 461808,
            NetFrameworkVersion.NetFx48 => LaunchConditionDefaults.NetFramework48Release,
            _ => 533320,
        };

        public static string NetFrameworkName(NetFrameworkVersion version) => version switch
        {
            NetFrameworkVersion.NetFx462 => ".NET Framework 4.6.2",
            NetFrameworkVersion.NetFx472 => ".NET Framework 4.7.2",
            NetFrameworkVersion.NetFx48 => ".NET Framework 4.8",
            _ => ".NET Framework 4.8.1",
        };

        public static RegistryPropertySearch NetFrameworkSearch() =>
            new RegistryPropertySearch(NetFrameworkReleaseProperty, RegistryHive.LocalMachine, NetFrameworkKey, "Release", win64: true);

        public static string NetFrameworkCondition(NetFrameworkVersion version) =>
            LaunchConditionDefaults.UnlessInstalled($"{NetFrameworkReleaseProperty} >= \"#{NetFrameworkRelease(version)}\"");

        public static RegistryPropertySearch[] WebView2Searches() => new[]
        {
            new RegistryPropertySearch(WebView2MachineProperty, RegistryHive.LocalMachine, WebView2Key, "pv", win64: false),
            new RegistryPropertySearch(WebView2UserProperty, RegistryHive.CurrentUser, WebView2Key, "pv", win64: false),
        };

        public static string WebView2Condition() => LaunchConditionDefaults.UnlessInstalled(
            $"({WebView2MachineProperty} <> \"\" AND {WebView2MachineProperty} <> \"0.0.0.0\") OR ({WebView2UserProperty} <> \"\" AND {WebView2UserProperty} <> \"0.0.0.0\")");
    }

    /// <summary>Conditions that the .NET Framework based parts of the engine add on their own.</summary>
    public static class LaunchConditionDefaults
    {
        /// <summary>The <c>Release</c> value of .NET Framework 4.8 in the registry.</summary>
        public const int NetFramework48Release = 528040;

        /// <summary>The message of the automatic .NET Framework 4.8 condition.</summary>
        public static string NetFramework48Message(string productName) =>
            $"{productName} needs the .NET Framework 4.8 to install. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48 (it is part of Windows 10 version 1903 and later), then run the setup again.";

        /// <summary>The message of a .NET Framework requirement.</summary>
        public static string NetFrameworkMessage(string frameworkName, string productName) =>
            $"{productName} needs the {frameworkName} to install. Install it from https://dotnet.microsoft.com/download/dotnet-framework, then run the setup again.";

        /// <summary>Wraps a condition so it is not checked when the product is already installed (repair, uninstall).</summary>
        public static string UnlessInstalled(string condition) => $"Installed OR ({condition})";
    }
}
