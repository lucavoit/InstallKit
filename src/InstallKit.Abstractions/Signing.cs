using System;
using System.Diagnostics;
using System.IO;

namespace InstallKit
{
    /// <summary>
    /// Signs a file in place. InstallKit decides <em>when</em> to sign: every MSI right after it is built
    /// (before a bundle embeds it), and for bundles the Burn engine and then the bundle itself.
    /// <em>How</em> to sign (signtool, AzureSignTool, Trusted Signing, a remote service, ...) is up to you.
    /// </summary>
    public interface IFileSigner
    {
        void Sign(string filePath);
    }

    /// <summary>
    /// Runs a command line per file, with <c>{file}</c> replaced by the quoted path. Set it with the
    /// <c>InstallKitSignCommand</c> MSBuild property or the <c>INSTALLKIT_SIGN_COMMAND</c> environment variable:
    /// <code>AzureSignTool sign -kvu $(KeyVaultUrl) -kvc MyCert -kvm -tr http://timestamp.digicert.com {file}</code>
    /// </summary>
    public sealed class CommandFileSigner : IFileSigner
    {
        public const string Placeholder = "{file}";

        public CommandFileSigner(string commandTemplate)
        {
            if (string.IsNullOrWhiteSpace(commandTemplate) || !commandTemplate.Contains(Placeholder))
                throw new ArgumentException($"The sign command must contain the {Placeholder} placeholder.", nameof(commandTemplate));

            CommandTemplate = commandTemplate;
        }

        public string CommandTemplate { get; }

        public void Sign(string filePath)
        {
            var command = CommandTemplate.Replace(Placeholder, "\"" + Path.GetFullPath(filePath) + "\"");
            Console.WriteLine($"InstallKit: signing {Path.GetFileName(filePath)}");

            var start = new ProcessStartInfo("cmd.exe", "/d /s /c \"" + command + "\"")
            {
                UseShellExecute = false,
            };

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the sign command.");
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"The sign command failed with exit code {process.ExitCode} for '{filePath}'.");
        }
    }
}
