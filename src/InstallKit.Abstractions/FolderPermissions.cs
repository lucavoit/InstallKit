using System;

namespace InstallKit
{
    /// <summary>How much access an account gets to a folder.</summary>
    public enum FolderRights
    {
        /// <summary>List the folder and read and run its files.</summary>
        Read,

        /// <summary>Read, run, create, change and delete files and subfolders (Windows' "Modify").</summary>
        Modify,

        /// <summary>Everything, including changing the permissions themselves.</summary>
        FullControl,
    }

    /// <summary>
    /// Gives an account access to a folder of the product, for example so a service running as a limited
    /// account can write to its data folder. A folder that holds no files of the product is created.
    /// </summary>
    public sealed class FolderPermission
    {
        public FolderPermission(string account, FolderRights rights, string? folder = null)
        {
            Account = account;
            Rights = rights;
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder!.Replace('/', '\\').Trim('\\');
        }

        /// <summary>
        /// The account: a name such as <c>Users</c>, <c>Everyone</c>, <c>Authenticated Users</c> or <c>Administrators</c>, or
        /// <c>DOMAIN\name</c> such as <c>NT AUTHORITY\LocalService</c> or <c>NT SERVICE\MyService</c>.
        /// </summary>
        public string Account { get; }

        public FolderRights Rights { get; }

        /// <summary>The folder below the install directory, or <c>null</c> for the install directory itself.</summary>
        public string? Folder { get; }

        /// <summary>The part of <see cref="Account"/> before the backslash, or <c>null</c>.</summary>
        public string? Domain
        {
            get
            {
                var slash = Account?.IndexOf('\\') ?? -1;
                return slash > 0 ? Account!.Substring(0, slash) : null;
            }
        }

        /// <summary>The account name without its domain.</summary>
        public string UserName
        {
            get
            {
                var slash = Account?.IndexOf('\\') ?? -1;
                return slash >= 0 ? Account!.Substring(slash + 1) : Account ?? string.Empty;
            }
        }

        internal bool HasValidFolder =>
            Folder == null ||
            (Folder.Length > 0 && Folder.IndexOf(':') < 0 && !Folder.StartsWith("\\", StringComparison.Ordinal) &&
             Array.TrueForAll(Folder.Split('\\'), part => part.Length > 0 && part != "." && part != ".." && part.IndexOfAny(new[] { '*', '?', '"', '<', '>', '|' }) < 0));
    }
}
