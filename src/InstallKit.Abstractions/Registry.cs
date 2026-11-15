using System;

namespace InstallKit
{
    /// <summary>The registry root a value is written under.</summary>
    public enum RegistryHive
    {
        /// <summary><c>HKEY_LOCAL_MACHINE</c>. Needs a per-machine install.</summary>
        LocalMachine,

        /// <summary><c>HKEY_CURRENT_USER</c>.</summary>
        CurrentUser,

        /// <summary><c>HKEY_CLASSES_ROOT</c>: per machine it maps to <c>HKLM\Software\Classes</c>, per user to <c>HKCU\Software\Classes</c>.</summary>
        ClassesRoot,

        /// <summary><c>HKEY_USERS</c>.</summary>
        Users,
    }

    /// <summary>The type of a registry value.</summary>
    public enum RegistryValueKind
    {
        /// <summary><c>REG_SZ</c>. Default.</summary>
        String,

        /// <summary><c>REG_EXPAND_SZ</c>: <c>%VARIABLES%</c> are expanded when the value is read.</summary>
        ExpandString,

        /// <summary><c>REG_DWORD</c>. The value must be a whole number.</summary>
        DWord,
    }

    /// <summary>
    /// A registry value written at install time and removed again on uninstall (Windows Installer removes
    /// the values of a component together with it). String values can use MSI formatted text such as
    /// <c>[INSTALLDIR]</c>.
    /// </summary>
    public sealed class RegistryEntry
    {
        public RegistryEntry(RegistryHive hive, string key, string? name, string value, RegistryValueKind kind = RegistryValueKind.String)
        {
            Hive = hive;
            Key = key;
            Name = string.IsNullOrEmpty(name) ? null : name;
            Value = value;
            Kind = kind;
        }

        public RegistryHive Hive { get; }

        /// <summary>The key path below the hive, for example <c>Software\Contoso\My App</c>.</summary>
        public string Key { get; }

        /// <summary>The value name, or <c>null</c> for the key's default value.</summary>
        public string? Name { get; }

        /// <summary>The value. For <see cref="RegistryValueKind.DWord"/> a whole number written as text.</summary>
        public string Value { get; }

        public RegistryValueKind Kind { get; }

        /// <summary>The value of a <see cref="RegistryValueKind.DWord"/> entry, or <c>null</c> when it is not a whole number.</summary>
        public int? DWordValue =>
            Kind == RegistryValueKind.DWord && int.TryParse(Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? number
                : (int?)null;

        /// <summary>For example <c>HKLM\Software\Contoso\My App\Mode</c>; used in messages.</summary>
        public override string ToString() =>
            $"{HiveName(Hive)}\\{Key}\\{Name ?? "(Default)"}";

        static string HiveName(RegistryHive hive) => hive switch
        {
            RegistryHive.LocalMachine => "HKLM",
            RegistryHive.CurrentUser => "HKCU",
            RegistryHive.ClassesRoot => "HKCR",
            _ => "HKU",
        };
    }
}
