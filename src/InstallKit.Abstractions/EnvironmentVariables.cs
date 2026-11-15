namespace InstallKit
{
    /// <summary>How an environment variable is changed.</summary>
    public enum EnvironmentVariableAction
    {
        /// <summary>Sets the whole value, replacing an existing one.</summary>
        Set,

        /// <summary>Adds the value to the end of the existing one, separated by <c>;</c>. Only the added part is removed on uninstall.</summary>
        Append,

        /// <summary>Adds the value to the start of the existing one, separated by <c>;</c>. Only the added part is removed on uninstall.</summary>
        Prepend,
    }

    /// <summary>
    /// An environment variable change made at install time and undone on uninstall. It is a machine-wide variable
    /// for per-machine installs and a user variable for per-user installs. Programs that are already running
    /// only see the change after they are restarted.
    /// </summary>
    public sealed class EnvironmentVariableEntry
    {
        public EnvironmentVariableEntry(string name, string value, EnvironmentVariableAction action = EnvironmentVariableAction.Set)
        {
            Name = name;
            Value = value;
            Action = action;
        }

        public string Name { get; }

        /// <summary>The value. MSI formatted text such as <c>[INSTALLDIR]bin</c> is expanded.</summary>
        public string Value { get; }

        public EnvironmentVariableAction Action { get; }

        /// <summary>Whether this entry changes <c>PATH</c>.</summary>
        public bool IsPath => string.Equals(Name, "PATH", System.StringComparison.OrdinalIgnoreCase);
    }
}
