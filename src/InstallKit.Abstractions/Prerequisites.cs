using System;
using System.Linq;

namespace InstallKit
{
    /// <summary>The processor architecture of a prerequisite.</summary>
    public enum PrerequisiteArchitecture
    {
        X64,
        X86,
        Arm64,
    }

    /// <summary>Which .NET runtime a bundle needs.</summary>
    public enum DotNetRuntimeKind
    {
        /// <summary>The base runtime (Microsoft.NETCore.App) for console programs and services.</summary>
        Runtime,

        /// <summary>The ASP.NET Core runtime (Microsoft.AspNetCore.App).</summary>
        AspNetCore,

        /// <summary>The Windows Desktop runtime (Microsoft.WindowsDesktop.App) for WPF and Windows Forms.</summary>
        WindowsDesktop,
    }

    /// <summary>
    /// Where the installer of a prerequisite comes from: a file you ship inside the setup (works offline), or a
    /// download at install time (a smaller setup, but Burn needs the file's exact SHA-512 hash and size).
    /// </summary>
    public sealed class PrerequisiteInstaller
    {
        PrerequisiteInstaller(string? filePath, string? url, string? sha512, long size, string? fileName)
        {
            FilePath = filePath;
            Url = url;
            Sha512 = sha512;
            Size = size;
            FileName = fileName;
        }

        /// <summary>The installer file, embedded into the setup. <c>null</c> for a download.</summary>
        public string? FilePath { get; }

        /// <summary>The download address (https). <c>null</c> for an embedded file.</summary>
        public string? Url { get; }

        /// <summary>The SHA-512 hash of the file at <see cref="Url"/> as 128 hex digits.</summary>
        public string? Sha512 { get; }

        /// <summary>The size in bytes of the file at <see cref="Url"/>.</summary>
        public long Size { get; }

        /// <summary>The name the downloaded file is saved as. Defaults to the last part of the URL.</summary>
        public string? FileName { get; }

        public bool IsDownload => Url != null;

        /// <summary>An installer file that is embedded into the setup.</summary>
        public static PrerequisiteInstaller FromFile(string path) => new PrerequisiteInstaller(path, null, null, 0, null);

        /// <summary>
        /// An installer that is downloaded when the setup runs. The setup refuses a file whose hash or size differs,
        /// so use the values of exactly the version the URL serves, and update them when Microsoft updates the file.
        /// </summary>
        /// <param name="url">The https address of the installer.</param>
        /// <param name="sha512">The SHA-512 hash as 128 hex digits (<c>Get-FileHash -Algorithm SHA512</c>).</param>
        /// <param name="sizeInBytes">The size of the file in bytes.</param>
        /// <param name="fileName">The name to save it as. Defaults to the last part of the URL.</param>
        public static PrerequisiteInstaller FromDownload(string url, string sha512, long sizeInBytes, string? fileName = null) =>
            new PrerequisiteInstaller(null, url, sha512, sizeInBytes, fileName);

        /// <summary>The file name used inside the bundle for a download.</summary>
        public string ResolvedFileName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FileName))
                    return FileName!;
                if (Url != null && Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                {
                    var last = uri.Segments.LastOrDefault()?.Trim('/');
                    if (!string.IsNullOrEmpty(last))
                        return last!;
                }

                return "prerequisite.exe";
            }
        }

        internal bool HasValidHash => Sha512 != null && Sha512.Length == 128 && Sha512.All(Uri.IsHexDigit);
    }

    /// <summary>
    /// A value the setup looks up on the machine before it decides what to install. Prerequisite helpers add these
    /// and refer to their <see cref="Variable"/> in their detect condition.
    /// </summary>
    public abstract class BundleSearch
    {
        protected BundleSearch(string variable) => Variable = variable;

        /// <summary>The Burn variable that receives the result.</summary>
        public string Variable { get; }
    }

    /// <summary>Reads a registry value into a Burn variable.</summary>
    public sealed class RegistryBundleSearch : BundleSearch
    {
        public RegistryBundleSearch(string variable, RegistryHive root, string key, string value, bool win64)
            : base(variable)
        {
            Root = root;
            Key = key;
            Value = value;
            Win64 = win64;
        }

        /// <summary>Only <see cref="RegistryHive.LocalMachine"/> and <see cref="RegistryHive.CurrentUser"/> are used.</summary>
        public RegistryHive Root { get; }

        public string Key { get; }

        public string Value { get; }

        /// <summary><c>true</c> reads the 64-bit view of the registry, <c>false</c> the 32-bit view.</summary>
        public bool Win64 { get; }
    }

    /// <summary>Finds the highest installed version of a .NET runtime with the given major version.</summary>
    public sealed class DotNetBundleSearch : BundleSearch
    {
        public DotNetBundleSearch(string variable, DotNetRuntimeKind kind, int majorVersion, PrerequisiteArchitecture architecture)
            : base(variable)
        {
            Kind = kind;
            MajorVersion = majorVersion;
            Architecture = architecture;
        }

        public DotNetRuntimeKind Kind { get; }

        public int MajorVersion { get; }

        public PrerequisiteArchitecture Architecture { get; }
    }
}
