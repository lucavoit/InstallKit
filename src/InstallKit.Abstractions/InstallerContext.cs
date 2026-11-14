using System;
using System.Collections.Generic;
using System.IO;

namespace InstallKit
{
    /// <summary>
    /// Build inputs handed to the installer program. The InstallKit SDK fills these in from MSBuild
    /// through environment variables; each value can also be passed on the command line so the
    /// program can be run on its own (<c>--version 1.2.3 --output out --payload MyApp=path</c>).
    /// </summary>
    public sealed class InstallerContext
    {
        public const string VersionVariable = "INSTALLKIT_VERSION";
        public const string OutputDirectoryVariable = "INSTALLKIT_OUTPUT_DIR";
        public const string IntermediateDirectoryVariable = "INSTALLKIT_INTERMEDIATE_DIR";
        public const string ConfigurationVariable = "INSTALLKIT_CONFIGURATION";
        public const string PayloadManifestVariable = "INSTALLKIT_PAYLOAD_MANIFEST";
        public const string OutputsFileVariable = "INSTALLKIT_OUTPUTS_FILE";
        public const string WixVersionVariable = "INSTALLKIT_WIX_VERSION";
        public const string EmitSourceVariable = "INSTALLKIT_EMIT_SOURCE";
        public const string SignCommandVariable = "INSTALLKIT_SIGN_COMMAND";
        public const string WixEulaVariable = "INSTALLKIT_WIX_ACCEPT_EULA";

        public InstallerContext(
            Version version,
            string outputDirectory,
            string intermediateDirectory,
            string configuration,
            IReadOnlyDictionary<string, string> payloads,
            string? outputsFile = null,
            string? wixVersion = null,
            bool emitSource = true,
            IFileSigner? signer = null,
            string? acceptedWixEula = null)
        {
            Version = version;
            OutputDirectory = outputDirectory;
            IntermediateDirectory = intermediateDirectory;
            Configuration = configuration;
            Payloads = payloads;
            OutputsFile = outputsFile;
            WixVersion = wixVersion;
            EmitSource = emitSource;
            Signer = signer;
            AcceptedWixEula = acceptedWixEula;
        }

        /// <summary>The product version, already normalised to a valid MSI version.</summary>
        public Version Version { get; }

        /// <summary>Where the .msi and setup .exe files are written.</summary>
        public string OutputDirectory { get; }

        /// <summary>Scratch directory (generated sources, logs).</summary>
        public string IntermediateDirectory { get; }

        public string Configuration { get; }

        /// <summary>Published payload directories keyed by payload name.</summary>
        public IReadOnlyDictionary<string, string> Payloads { get; }

        /// <summary>File the built output paths are written to, one per line, so MSBuild can pick them up.</summary>
        public string? OutputsFile { get; }

        /// <summary>The pinned WiX toolset version; WiX extensions are pinned to the same version.</summary>
        public string? WixVersion { get; }

        /// <summary>
        /// The WiX EULA you accept, for example <c>wix7</c>. WiX 7 and later refuse to build without it.
        /// Set with the <c>InstallKitAcceptWixEula</c> MSBuild property.
        /// </summary>
        public string? AcceptedWixEula { get; }

        /// <summary>Keep the generated WiX source next to the output so changes can be reviewed.</summary>
        public bool EmitSource { get; }

        /// <summary>
        /// Signs built files, or <c>null</c> to leave them unsigned. Set from <c>InstallKitSignCommand</c>,
        /// or in code with <see cref="InstallerPlan.UseSigner"/>.
        /// </summary>
        public IFileSigner? Signer { get; internal set; }

        public bool IsDebug => string.Equals(Configuration, "Debug", StringComparison.OrdinalIgnoreCase);

        /// <summary>Resolves a payload name to its directory, or <c>null</c> when it is unknown.</summary>
        public string? TryGetPayloadDirectory(string payloadName) =>
            Payloads.TryGetValue(payloadName, out var directory) ? directory : null;

        /// <summary>Creates a context from the environment and command line.</summary>
        public static InstallerContext FromEnvironment(string[] args)
        {
            var options = ParseArgs(args);

            string? Get(string option, string variable) =>
                options.TryGetValue(option, out var value) ? value : Environment.GetEnvironmentVariable(variable);

            var rawVersion = Get("version", VersionVariable) ?? "1.0.0";
            if (!ProductVersion.TryParse(rawVersion, out var version, out var versionError))
                throw new InstallerValidationException(new[] { new ValidationError("IK001", versionError!) });

            var output = Path.GetFullPath(Get("output", OutputDirectoryVariable) ?? Path.Combine("bin", "installer"));
            var intermediate = Path.GetFullPath(Get("intermediate", IntermediateDirectoryVariable) ?? Path.Combine("obj", "installer"));
            var configuration = Get("configuration", ConfigurationVariable) ?? "Release";

            var payloads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var manifest = Get("payload-manifest", PayloadManifestVariable);
            if (!string.IsNullOrEmpty(manifest) && File.Exists(manifest))
            {
                foreach (var line in File.ReadAllLines(manifest!))
                    AddPayload(payloads, line);
            }

            if (options.TryGetValue("payload", out var payloadOptions))
            {
                foreach (var entry in payloadOptions.Split(';'))
                    AddPayload(payloads, entry);
            }

            var emit = Get("emit-source", EmitSourceVariable);
            var signCommand = Get("sign-command", SignCommandVariable);

            return new InstallerContext(
                version,
                output,
                intermediate,
                configuration,
                payloads,
                Get("outputs-file", OutputsFileVariable),
                Get("wix-version", WixVersionVariable),
                emitSource: !string.Equals(emit, "false", StringComparison.OrdinalIgnoreCase),
                signer: string.IsNullOrWhiteSpace(signCommand) ? null : new CommandFileSigner(signCommand!),
                acceptedWixEula: NullIfEmpty(Get("accept-wix-eula", WixEulaVariable)));
        }

        static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

        static void AddPayload(IDictionary<string, string> payloads, string entry)
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0)
                return;

            var name = entry.Substring(0, separator).Trim();
            var directory = entry.Substring(separator + 1).Trim();
            if (name.Length > 0 && directory.Length > 0)
                payloads[name] = Path.GetFullPath(directory);
        }

        internal static Dictionary<string, string> ParseArgs(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                    continue;

                var key = arg.Substring(2);
                string value;
                var equals = key.IndexOf('=');
                if (equals >= 0)
                {
                    value = key.Substring(equals + 1);
                    key = key.Substring(0, equals);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }
                else
                {
                    value = "true";
                }

                // Repeated options (for example several --payload) are joined with ';'.
                options[key] = options.TryGetValue(key, out var existing) ? existing + ";" + value : value;
            }

            return options;
        }
    }
}
