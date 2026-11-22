using System;
using System.Collections.Generic;

namespace InstallKit
{
    /// <summary>
    /// A machine-wide folder outside the install directory, for example <c>%ProgramData%\Vendor\App</c>, that the MSI creates
    /// with the given access before any install event handler or service runs. Declare it with <see cref="MachineBuilder.DataFolder"/>.
    /// </summary>
    public sealed class DataFolder
    {
        public DataFolder(Environment.SpecialFolder root, string path)
        {
            Root = root;
            Path = (path ?? string.Empty).Replace('/', '\\').Trim('\\');
        }

        /// <summary>The well-known folder the path is below. Only <see cref="Environment.SpecialFolder.CommonApplicationData"/> (<c>%ProgramData%</c>) is supported.</summary>
        public Environment.SpecialFolder Root { get; }

        /// <summary>The folder below <see cref="Root"/>, for example <c>Contoso\My App</c>.</summary>
        public string Path { get; }

        /// <summary>Who gets which access. Accounts that are not listed keep what the parent folder gives them.</summary>
        public IList<FolderPermission> Access { get; } = new List<FolderPermission>();

        /// <summary>The MSI standard directory of <see cref="Root"/>.</summary>
        public string RootDirectoryId => "CommonAppDataFolder";

        internal bool HasValidPath =>
            Path.Length > 0 && Path.IndexOf(':') < 0 &&
            Array.TrueForAll(Path.Split('\\'), part => part.Length > 0 && part != "." && part != ".." && part.IndexOfAny(new[] { '*', '?', '"', '<', '>', '|', '%', '[', ']' }) < 0);
    }

    /// <summary>Who may do what in a <see cref="DataFolder"/>. Used by <see cref="MachineBuilder.DataFolder"/>.</summary>
    public sealed class DataFolderBuilder
    {
        readonly DataFolder _folder;

        internal DataFolderBuilder(DataFolder folder) => _folder = folder;

        /// <summary>Gives an account access to the folder. See <see cref="FolderPermission"/> for the account names.</summary>
        public DataFolderBuilder Allow(string account, FolderRights rights)
        {
            _folder.Access.Add(new FolderPermission(account, rights));
            return this;
        }
    }
}
