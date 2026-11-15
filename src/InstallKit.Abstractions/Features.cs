namespace InstallKit
{
    /// <summary>
    /// An optional part of the product (documentation, samples, plug-ins) that can be left out. Add files to it with the
    /// <c>feature</c> argument of <c>WithPayload</c> and <c>WithDirectory</c>. Everything not assigned to a feature is always installed.
    /// </summary>
    public sealed class OptionalFeature
    {
        public OptionalFeature(string name, string? description = null, bool installedByDefault = true)
        {
            Name = name;
            Description = description;
            InstalledByDefault = installedByDefault;
        }

        /// <summary>The name users see in the feature tree. Also what <c>ADDLOCAL</c> and <c>REMOVE</c> on the command line refer to (without spaces).</summary>
        public string Name { get; }

        public string? Description { get; }

        /// <summary>Whether the feature is installed when the user does not choose. Default <c>true</c>.</summary>
        public bool InstalledByDefault { get; }

        /// <summary>The name as an MSI identifier: letters, digits and underscores, starting with a letter or underscore.</summary>
        public string Id
        {
            get
            {
                var id = new System.Text.StringBuilder();
                foreach (var c in Name)
                    id.Append(char.IsLetterOrDigit(c) && c < 128 || c == '_' ? c : '_');
                if (id.Length == 0 || char.IsDigit(id[0]))
                    id.Insert(0, '_');
                return id.ToString();
            }
        }
    }
}
