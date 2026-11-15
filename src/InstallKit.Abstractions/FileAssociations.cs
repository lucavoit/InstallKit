using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace InstallKit
{
    /// <summary>
    /// Makes a file extension open with one of the product's programs (a ProgId, the extension's default
    /// value and an <c>open</c> verb). Written as registry values, so uninstall removes it again.
    /// </summary>
    public sealed class FileAssociation
    {
        public FileAssociation(string extension, string description, string? executable = null, string? progId = null)
        {
            Extension = extension;
            Description = description;
            Executable = executable;
            ProgId = progId;
        }

        /// <summary>The extension with its dot, for example <c>.foo</c>.</summary>
        public string Extension { get; }

        /// <summary>The file type name Explorer shows, for example <c>Foo document</c>.</summary>
        public string Description { get; }

        /// <summary>The program that opens the files, relative to the install directory. Defaults to <see cref="MsiDefinition.MainExecutable"/>.</summary>
        public string? Executable { get; }

        /// <summary>The ProgId. Defaults to the output file name plus the extension, for example <c>MyApp.foo</c>.</summary>
        public string? ProgId { get; }

        internal static bool IsValidExtension(string extension) => Regex.IsMatch(extension ?? string.Empty, @"^\.[A-Za-z0-9_+\-]+$");

        internal static bool IsValidProgId(string progId) => Regex.IsMatch(progId ?? string.Empty, @"^[A-Za-z][A-Za-z0-9_.]*$");

        internal string ResolveProgId(MsiDefinition msi) =>
            string.IsNullOrWhiteSpace(ProgId) ? Regex.Replace(msi.ResolvedOutputFileName, @"[^A-Za-z0-9_]", string.Empty) + Extension : ProgId!;

        internal string ResolveExecutable(MsiDefinition msi) =>
            string.IsNullOrWhiteSpace(Executable) ? msi.MainExecutable ?? string.Empty : Executable!;

        internal IEnumerable<RegistryEntry> ToRegistryEntries(MsiDefinition msi)
        {
            var progId = ResolveProgId(msi);
            var exe = "[INSTALLDIR]" + ResolveExecutable(msi).Replace('/', '\\').TrimStart('\\');

            yield return new RegistryEntry(RegistryHive.ClassesRoot, Extension, null, progId);
            yield return new RegistryEntry(RegistryHive.ClassesRoot, progId, null, Description);
            yield return new RegistryEntry(RegistryHive.ClassesRoot, progId + @"\DefaultIcon", null, $"\"{exe}\",0");
            yield return new RegistryEntry(RegistryHive.ClassesRoot, progId + @"\shell\open\command", null, $"\"{exe}\" \"%1\"");
        }
    }

    /// <summary>
    /// Registers a URL scheme such as <c>myapp://open?id=1</c> that starts one of the product's programs with
    /// the full URL as its only argument. Written as registry values, so uninstall removes it again.
    /// </summary>
    public sealed class UrlProtocol
    {
        public UrlProtocol(string scheme, string description, string? executable = null)
        {
            Scheme = scheme;
            Description = description;
            Executable = executable;
        }

        /// <summary>The scheme without <c>://</c>, for example <c>myapp</c>.</summary>
        public string Scheme { get; }

        /// <summary>Shown to the user when the browser asks which program should open the link.</summary>
        public string Description { get; }

        /// <summary>The program that handles the URL, relative to the install directory. Defaults to <see cref="MsiDefinition.MainExecutable"/>.</summary>
        public string? Executable { get; }

        internal static bool IsValidScheme(string scheme) => Regex.IsMatch(scheme ?? string.Empty, @"^[A-Za-z][A-Za-z0-9+.\-]*$");

        internal string ResolveExecutable(MsiDefinition msi) =>
            string.IsNullOrWhiteSpace(Executable) ? msi.MainExecutable ?? string.Empty : Executable!;

        internal IEnumerable<RegistryEntry> ToRegistryEntries(MsiDefinition msi)
        {
            var exe = "[INSTALLDIR]" + ResolveExecutable(msi).Replace('/', '\\').TrimStart('\\');

            yield return new RegistryEntry(RegistryHive.ClassesRoot, Scheme, null, "URL:" + Description);
            yield return new RegistryEntry(RegistryHive.ClassesRoot, Scheme, "URL Protocol", string.Empty);
            yield return new RegistryEntry(RegistryHive.ClassesRoot, Scheme + @"\DefaultIcon", null, $"\"{exe}\",0");
            yield return new RegistryEntry(RegistryHive.ClassesRoot, Scheme + @"\shell\open\command", null, $"\"{exe}\" \"%1\"");
        }
    }
}
