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

    /// <summary>Conditions that the .NET Framework based parts of the engine add on their own.</summary>
    public static class LaunchConditionDefaults
    {
        /// <summary>The <c>Release</c> value of .NET Framework 4.8 in the registry.</summary>
        public const int NetFramework48Release = 528040;

        /// <summary>The message of the automatic .NET Framework 4.8 condition.</summary>
        public static string NetFramework48Message(string productName) =>
            $"{productName} needs the .NET Framework 4.8 to install. Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48 (it is part of Windows 10 version 1903 and later), then run the setup again.";

        /// <summary>Wraps a condition so it is not checked when the product is already installed (repair, uninstall).</summary>
        public static string UnlessInstalled(string condition) => $"Installed OR ({condition})";
    }
}
