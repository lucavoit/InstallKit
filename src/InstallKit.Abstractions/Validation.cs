using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstallKit
{
    public sealed class ValidationError
    {
        public ValidationError(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }

        /// <summary>MSBuild canonical format, so errors show up in Visual Studio, the CLI and Azure DevOps.</summary>
        public override string ToString() => $"InstallKit : error {Code}: {Message}";
    }

    public sealed class InstallerValidationException : Exception
    {
        public InstallerValidationException(IReadOnlyList<ValidationError> errors)
            : base(string.Join(Environment.NewLine, errors.Select(e => e.ToString())))
            => Errors = errors;

        public IReadOnlyList<ValidationError> Errors { get; }
    }

    /// <summary>Checks a plan before any WiX tooling runs, with messages that say how to fix the problem.</summary>
    public static class InstallerValidator
    {
        public static IReadOnlyList<ValidationError> Validate(InstallerPlan plan)
        {
            var errors = new List<ValidationError>();
            var context = plan.Context;

            if (plan.Msis.Count == 0 && plan.Bundles.Count == 0)
                errors.Add(new ValidationError("IK100", "The installer plan is empty. Add at least one plan.Msi(...) or plan.Bundle(...)."));

            var upgradeCodes = new Dictionary<Guid, string>();

            foreach (var msi in plan.Msis)
            {
                var label = $"MSI '{msi.Name}'";
                CheckUpgradeCode(errors, upgradeCodes, label, msi.UpgradeCode);

                if (msi.Payloads.Count == 0)
                    errors.Add(new ValidationError("IK110", $"{label} has no files. Add msi.AddPayload(\"MyApp\") or msi.AddDirectory(...)."));

                foreach (var payload in msi.Payloads)
                {
                    var directory = ResolvePayload(payload, context);
                    if (payload.PayloadName != null && directory == null)
                    {
                        var known = context.Payloads.Count == 0 ? "none" : string.Join(", ", context.Payloads.Keys);
                        errors.Add(new ValidationError("IK111",
                            $"{label} uses payload '{payload.PayloadName}', but the project has no <InstallerPayload> with that name (known: {known})."));
                    }
                    else if (directory != null && !Directory.Exists(directory))
                    {
                        errors.Add(new ValidationError("IK112", $"{label}: payload directory '{directory}' does not exist."));
                    }
                }

                CheckInstalledFile(errors, msi, context, label, msi.MainExecutable, "main executable");
                foreach (var service in msi.Services)
                    CheckInstalledFile(errors, msi, context, label, service.Executable, $"service '{service.Name}'");

                if (msi.Shortcuts.Desktop && string.IsNullOrWhiteSpace(msi.MainExecutable))
                    errors.Add(new ValidationError("IK120", $"{label} asks for a desktop shortcut but has no MainExecutable."));

                CheckOptionalFile(errors, label, msi.LicenseFile, "license file", ".rtf");
                CheckOptionalFile(errors, label, msi.IconFile, "icon", ".ico");
                CheckBranding(errors, label, msi.Branding);

                foreach (var handler in msi.EventHandlers)
                {
                    var problem = handler.GetInstallTimeProblem();
                    if (problem != null)
                        errors.Add(new ValidationError("IK150", $"{label}: the {handler.Stage} handler '{handler.Handler.Method.Name}' cannot run at install time: {problem}."));
                }

                CheckRegistry(errors, msi, label);

                foreach (var command in msi.Commands)
                {
                    if (string.IsNullOrWhiteSpace(command.Path))
                        errors.Add(new ValidationError("IK151", $"{label}: a {command.Timing} command has no path."));
                }
            }

            foreach (var bundle in plan.Bundles)
            {
                var label = $"bundle '{bundle.Name}'";
                CheckUpgradeCode(errors, upgradeCodes, label, bundle.UpgradeCode);

                if (bundle.Chain.Items.Count == 0)
                    errors.Add(new ValidationError("IK130", $"{label} has an empty chain. Add bundle.Chain.Msi(...) or another package."));

                foreach (var item in bundle.Chain.Items)
                {
                    switch (item)
                    {
                        case MsiChainItem m when m.Definition != null && !plan.Msis.Contains(m.Definition):
                            errors.Add(new ValidationError("IK131", $"{label} chains MSI '{m.Definition.Name}', which is not part of this plan."));
                            break;
                        case MsiChainItem m when m.Path != null && !File.Exists(m.Path):
                            errors.Add(new ValidationError("IK132", $"{label} chains '{m.Path}', which does not exist."));
                            break;
                        case ExeChainItem e when !File.Exists(e.Path):
                            errors.Add(new ValidationError("IK133", $"{label} chains '{e.Path}', which does not exist."));
                            break;
                        case ExeChainItem e when string.IsNullOrWhiteSpace(e.DetectCondition):
                            errors.Add(new ValidationError("IK134",
                                $"{label}: '{Path.GetFileName(e.Path)}' has no DetectCondition, so it would run on every install. " +
                                "Set one, for example a registry or file search result."));
                            break;
                    }
                }

                if (bundle.License != null && !IsUrl(bundle.License))
                    CheckOptionalFile(errors, label, bundle.License, "license file", ".rtf");
                CheckOptionalFile(errors, label, bundle.IconFile, "icon", ".ico");
                CheckBranding(errors, label, bundle.Branding);
            }

            return errors;
        }

        /// <summary>Resolves a payload to a directory. Returns <c>null</c> for an unknown payload name.</summary>
        public static string? ResolvePayload(PayloadSource payload, InstallerContext context) =>
            payload.PayloadName != null
                ? context.TryGetPayloadDirectory(payload.PayloadName)
                : Path.GetFullPath(payload.Directory!);

        /// <summary>
        /// Finds the source file of a path relative to the install directory, by looking in the payloads
        /// whose target folder matches. Returns the payload and full source path, or <c>null</c>.
        /// </summary>
        public static (PayloadSource Payload, string SourcePath)? FindInstalledFile(MsiDefinition msi, InstallerContext context, string installedPath)
        {
            var normalized = installedPath.Replace('/', '\\').TrimStart('\\');
            var folder = Path.GetDirectoryName(normalized)?.Replace('/', '\\');
            folder = string.IsNullOrEmpty(folder) ? null : folder;

            foreach (var payload in msi.Payloads)
            {
                if (!string.Equals(payload.TargetSubdirectory?.Replace('/', '\\'), folder, StringComparison.OrdinalIgnoreCase))
                    continue;

                var directory = ResolvePayload(payload, context);
                if (directory == null)
                    continue;

                var source = Path.Combine(directory, Path.GetFileName(normalized));
                if (File.Exists(source))
                    return (payload, source);
            }

            return null;
        }

        static void CheckRegistry(List<ValidationError> errors, MsiDefinition msi, string label)
        {
            foreach (var entry in msi.RegistryEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.StartsWith("\\", StringComparison.Ordinal))
                    errors.Add(new ValidationError("IK170", $"{label}: a registry value under {entry.Hive} has the key '{entry.Key}'. Use a path below the hive without a leading backslash, for example Software\\Contoso\\App."));
                else if (entry.Kind == RegistryValueKind.DWord && entry.DWordValue == null)
                    errors.Add(new ValidationError("IK171", $"{label}: {entry} is a DWord but its value '{entry.Value}' is not a whole number."));

                if (entry.Hive == RegistryHive.LocalMachine && msi.Scope == InstallScope.PerUser)
                    errors.Add(new ValidationError("IK172", $"{label}: {entry} is under HKLM, which a per-user install cannot write. Use RegistryHive.CurrentUser, or install per machine."));
            }
        }

        static void CheckInstalledFile(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label, string? path, string what)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (FindInstalledFile(msi, context, path!) == null)
                errors.Add(new ValidationError("IK121",
                    $"{label}: the {what} '{path}' is not in any payload. It must be at the top level of a payload's target folder."));
        }

        static void CheckUpgradeCode(List<ValidationError> errors, Dictionary<Guid, string> seen, string label, string? upgradeCode)
        {
            if (string.IsNullOrWhiteSpace(upgradeCode))
            {
                errors.Add(new ValidationError("IK101",
                    $"{label} has no UpgradeCode. Generate one once and keep it forever, for example: UpgradeCode = \"{Guid.NewGuid():D}\"."));
                return;
            }

            if (!Guid.TryParse(upgradeCode, out var guid) || guid == Guid.Empty)
            {
                errors.Add(new ValidationError("IK102", $"{label}: UpgradeCode '{upgradeCode}' is not a valid GUID."));
                return;
            }

            if (seen.TryGetValue(guid, out var other))
                errors.Add(new ValidationError("IK103", $"{label} uses the same UpgradeCode as {other}. Every MSI and bundle needs its own."));
            else
                seen[guid] = label;
        }

        static void CheckBranding(List<ValidationError> errors, string label, Branding? branding)
        {
            if (branding == null)
                return;

            if (!string.IsNullOrWhiteSpace(branding.AccentColor) && !IsHexColor(branding.AccentColor!))
                errors.Add(new ValidationError("IK152", $"{label}: accent colour '{branding.AccentColor}' must look like #0067C0."));

            if (!string.IsNullOrWhiteSpace(branding.LogoFile))
            {
                if (!File.Exists(branding.LogoFile))
                    errors.Add(new ValidationError("IK140", $"{label}: logo '{branding.LogoFile}' does not exist."));
                else if (!new[] { ".png", ".ico", ".bmp", ".jpg", ".jpeg" }.Contains(Path.GetExtension(branding.LogoFile!).ToLowerInvariant()))
                    errors.Add(new ValidationError("IK141", $"{label}: logo '{branding.LogoFile}' must be a .png, .ico, .bmp or .jpg file."));
            }

            CheckOptionalFile(errors, label, branding.BannerImage, "banner image", ".bmp");
            CheckOptionalFile(errors, label, branding.BackgroundImage, "background image", ".bmp");
        }

        internal static bool IsHexColor(string value) =>
            value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

        static void CheckOptionalFile(List<ValidationError> errors, string label, string? path, string what, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!File.Exists(path))
                errors.Add(new ValidationError("IK140", $"{label}: {what} '{path}' does not exist."));
            else if (!path!.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                errors.Add(new ValidationError("IK141", $"{label}: {what} '{path}' must be a {extension} file."));
        }

        static bool IsUrl(string value) =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
