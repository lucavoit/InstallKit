using System;

namespace InstallKit
{
    /// <summary>
    /// A program that must not be running while the product is installed, upgraded or removed. Windows Installer
    /// asks it to close (and can end it), so its files are not locked and the user is not asked to restart.
    /// </summary>
    public sealed class CloseApplicationRule
    {
        public CloseApplicationRule(string processName, bool terminate = false, int timeoutSeconds = 5)
        {
            ProcessName = processName;
            Terminate = terminate;
            TimeoutSeconds = timeoutSeconds;
        }

        /// <summary>The process's file name, for example <c>MyApp.exe</c>.</summary>
        public string ProcessName { get; }

        /// <summary>End the process if it is still running after it was asked to close. Default <c>false</c>.</summary>
        public bool Terminate { get; }

        /// <summary>How long to wait for the program to close before it is ended (when <see cref="Terminate"/> is set). Default 5.</summary>
        public int TimeoutSeconds { get; }

        internal bool IsProcessFileName =>
            !string.IsNullOrWhiteSpace(ProcessName) && ProcessName.IndexOfAny(new[] { '\\', '/', ':' }) < 0 &&
            ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && ProcessName.Length > ".exe".Length;
    }
}
