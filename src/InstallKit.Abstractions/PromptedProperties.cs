using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace InstallKit
{
    /// <summary>
    /// A value the modern setup dialogs ask the user for, kept in an MSI property. It has a default, can be given on the
    /// command line (<c>msiexec /i App.msi SERVER=prod</c>, which skips the question) and is available to everything that
    /// reads properties: <c>[SERVER]</c> in XML values and run commands, and <c>InstallEvent["SERVER"]</c> in install events.
    /// </summary>
    public sealed class PromptedProperty
    {
        public PromptedProperty(string name, string label, string? defaultValue = null, bool required = false)
        {
            Name = name;
            Label = label;
            DefaultValue = string.IsNullOrEmpty(defaultValue) ? null : defaultValue;
            Required = required;
        }

        /// <summary>The property name: capital letters, digits and underscores, for example <c>SERVER</c> (MSI public properties are upper case).</summary>
        public string Name { get; }

        /// <summary>The text above the input box, for example <c>Server address</c>.</summary>
        public string Label { get; }

        /// <summary>The value the box starts with, or <c>null</c> for an empty box.</summary>
        public string? DefaultValue { get; }

        /// <summary>The Next or Install button stays disabled while the box is empty.</summary>
        public bool Required { get; }

        /// <summary>The most prompts that fit on the page.</summary>
        public const int MaxPerInstaller = 3;

        internal bool HasValidName => Regex.IsMatch(Name ?? string.Empty, @"^[A-Z_][A-Z0-9_]*$");

        /// <summary>Properties that Windows Installer and InstallKit use themselves.</summary>
        static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "INSTALLDIR", "TARGETDIR", "ALLUSERS", "ADDLOCAL", "ADDDEFAULT", "REMOVE", "REINSTALL", "REINSTALLMODE", "REBOOT",
            "ARPNOMODIFY", "ARPNOREMOVE", "ARPNOREPAIR", "TRANSFORMS", "PATCH", "MSINEWINSTANCE", "INSTALLLEVEL", "PRODUCTCODE",
            "UPGRADINGPRODUCTCODE", "INSTALLKITLICENSEACCEPTED",
        };

        internal bool IsReserved => Reserved.Contains(Name ?? string.Empty);
    }
}
