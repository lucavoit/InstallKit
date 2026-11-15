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

                CheckFeatures(errors, msi, context, label);
                CheckRegistry(errors, msi, label);
                CheckEnvironment(errors, label, msi);
                CheckAssociations(errors, msi, context, label);
                CheckFirewall(errors, msi, context, label);
                CheckXmlValues(errors, msi, context, label);
                CheckCloseApplications(errors, msi, label);
                CheckFolderPermissions(errors, msi, label);

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

        static void CheckFeatures(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label)
        {
            var declared = new Dictionary<string, OptionalFeature>(StringComparer.OrdinalIgnoreCase);
            foreach (var feature in msi.Features)
            {
                if (string.IsNullOrWhiteSpace(feature.Name) || string.Equals(feature.Id, "Complete", StringComparison.OrdinalIgnoreCase))
                    errors.Add(new ValidationError("IK240", $"{label}: '{feature.Name}' is not a usable feature name. It must not be empty or 'Complete', which InstallKit uses for the files that are always installed."));
                else if (declared.ContainsKey(feature.Id))
                    errors.Add(new ValidationError("IK241", $"{label} declares the feature '{feature.Name}' more than once (names that differ only in spaces or punctuation are the same feature)."));
                else
                    declared[feature.Id] = feature;
            }

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var payload in msi.Payloads.Where(p => p.Feature != null))
            {
                var id = new OptionalFeature(payload.Feature!).Id;
                if (!declared.ContainsKey(id))
                    errors.Add(new ValidationError("IK242", $"{label}: files are assigned to the feature '{payload.Feature}', which is not declared. Add .WithFeature(\"{payload.Feature}\")."));
                used.Add(id);
            }

            foreach (var feature in declared.Values.Where(f => !used.Contains(f.Id)))
                errors.Add(new ValidationError("IK243", $"{label}: the feature '{feature.Name}' has no files. Pass feature: \"{feature.Name}\" to WithPayload or WithDirectory."));
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

        static void CheckEnvironment(List<ValidationError> errors, string label, MsiDefinition msi)
        {
            foreach (var variable in msi.EnvironmentVariables)
            {
                if (string.IsNullOrWhiteSpace(variable.Name) || variable.Name.IndexOf('=') >= 0)
                    errors.Add(new ValidationError("IK180", $"{label}: '{variable.Name}' is not a valid environment variable name (it must not be empty or contain '=')."));
                else if (string.IsNullOrWhiteSpace(variable.Value))
                    errors.Add(new ValidationError("IK181", $"{label}: the environment variable '{variable.Name}' has no value."));
                else if (variable.IsPath && variable.Action == EnvironmentVariableAction.Set)
                    errors.Add(new ValidationError("IK182", $"{label}: setting PATH would replace the whole variable. Use WithPathEntry(...) to add a folder, or WithEnvironmentVariable(\"PATH\", ..., EnvironmentVariableAction.Append)."));
                else if (variable.Action != EnvironmentVariableAction.Set && variable.Value.IndexOf(';') >= 0)
                    errors.Add(new ValidationError("IK183", $"{label}: the {variable.Name} entry '{variable.Value}' contains ';'. Add one folder per entry."));
            }
        }

        static void CheckAssociations(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var association in msi.FileAssociations)
            {
                if (!FileAssociation.IsValidExtension(association.Extension))
                    errors.Add(new ValidationError("IK190", $"{label}: '{association.Extension}' is not a valid file extension. Write it with its dot, for example \".foo\"."));
                else if (!extensions.Add(association.Extension))
                    errors.Add(new ValidationError("IK193", $"{label} associates '{association.Extension}' more than once."));

                if (!string.IsNullOrWhiteSpace(association.ProgId) && !FileAssociation.IsValidProgId(association.ProgId!))
                    errors.Add(new ValidationError("IK194", $"{label}: the ProgId '{association.ProgId}' must start with a letter and contain only letters, digits, '.' and '_'."));

                CheckHandler(errors, msi, context, label, association.ResolveExecutable(msi), $"file association '{association.Extension}'");
            }

            var schemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var protocol in msi.UrlProtocols)
            {
                if (!UrlProtocol.IsValidScheme(protocol.Scheme))
                    errors.Add(new ValidationError("IK191", $"{label}: '{protocol.Scheme}' is not a valid URL scheme. Use letters, digits, '+', '-' and '.', starting with a letter, and no '://'."));
                else if (!schemes.Add(protocol.Scheme))
                    errors.Add(new ValidationError("IK195", $"{label} registers the URL scheme '{protocol.Scheme}' more than once."));

                CheckHandler(errors, msi, context, label, protocol.ResolveExecutable(msi), $"URL protocol '{protocol.Scheme}'");
            }
        }

        static void CheckFirewall(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label)
        {
            foreach (var rule in msi.FirewallRules)
            {
                var what = $"firewall rule '{rule.Name}'";
                if (string.IsNullOrWhiteSpace(rule.Name))
                    errors.Add(new ValidationError("IK210", $"{label}: a firewall rule has no name."));
                else if (rule.Port == null && rule.Executable == null)
                    errors.Add(new ValidationError("IK211", $"{label}: the {what} names neither a port nor a program. Give it at least one."));

                if (!rule.HasValidPort)
                    errors.Add(new ValidationError("IK212", $"{label}: the {what} has the port '{rule.Port}'. Use a number from 1 to 65535, a range such as 8000-8100, or a comma separated list."));
                else if (rule.Port != null && rule.Protocol == FirewallProtocol.Any)
                    errors.Add(new ValidationError("IK213", $"{label}: the {what} has a port, so its protocol must be Tcp or Udp."));

                if (rule.Executable != null)
                    CheckInstalledFile(errors, msi, context, label, rule.Executable, what);

                if (msi.Scope == InstallScope.PerUser)
                    errors.Add(new ValidationError("IK214", $"{label}: the {what} cannot be created by a per-user install, which does not run elevated. Install per machine."));
            }
        }

        static void CheckXmlValues(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label)
        {
            foreach (var edit in msi.XmlValues)
            {
                var what = $"XML value '{edit.XPath}' in '{edit.File}'";
                if (string.IsNullOrWhiteSpace(edit.File))
                {
                    errors.Add(new ValidationError("IK220", $"{label}: an XML value has no file."));
                    continue;
                }

                if (string.Equals(Path.GetExtension(edit.File), ".json", StringComparison.OrdinalIgnoreCase))
                    errors.Add(new ValidationError("IK222", $"{label}: {what}: WithXmlValue edits XML files only. JSON files are not supported yet."));
                else
                    CheckInstalledFile(errors, msi, context, label, edit.File, $"file of the {what}");

                if (!edit.HasValidXPath)
                    errors.Add(new ValidationError("IK221", $"{label}: the XPath '{edit.XPath}' for '{edit.File}' is not valid, for example /configuration/appSettings/add[@key='Server']/@value."));
                else if (edit.Value == null)
                    errors.Add(new ValidationError("IK223", $"{label}: the {what} has no value."));
            }
        }

        static void CheckCloseApplications(List<ValidationError> errors, MsiDefinition msi, string label)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in msi.CloseApplications)
            {
                if (!rule.IsProcessFileName)
                    errors.Add(new ValidationError("IK230", $"{label}: '{rule.ProcessName}' is not a process file name. Use the name of the program's executable, for example MyApp.exe, without a path."));
                else if (!seen.Add(rule.ProcessName))
                    errors.Add(new ValidationError("IK231", $"{label} closes '{rule.ProcessName}' more than once."));

                if (rule.TimeoutSeconds < 1 || rule.TimeoutSeconds > 600)
                    errors.Add(new ValidationError("IK232", $"{label}: the timeout for closing '{rule.ProcessName}' is {rule.TimeoutSeconds} seconds. Use 1 to 600."));
            }
        }

        static void CheckFolderPermissions(List<ValidationError> errors, MsiDefinition msi, string label)
        {
            var payloadFolders = msi.Payloads.Where(p => p.TargetSubdirectory != null).Select(p => p.TargetSubdirectory!.Replace('/', '\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var permission in msi.FolderPermissions)
            {
                var where = permission.Folder == null ? "the install directory" : $"the folder '{permission.Folder}'";

                if (string.IsNullOrWhiteSpace(permission.Account) || permission.UserName.Length == 0)
                    errors.Add(new ValidationError("IK250", $"{label}: a folder permission for {where} has no account. Use a name such as Users or a DOMAIN\\name such as NT AUTHORITY\\LocalService."));

                if (!permission.HasValidFolder)
                {
                    errors.Add(new ValidationError("IK251", $"{label}: '{permission.Folder}' is not a folder below the install directory. Use a relative path such as data or data\\logs, without '..', a drive or wildcards."));
                    continue;
                }

                // A folder inside a payload folder would be created twice: once by the payload, once by the permission.
                if (permission.Folder != null && payloadFolders.Any(f => permission.Folder.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase)))
                    errors.Add(new ValidationError("IK252", $"{label}: {where} is inside a payload folder, which is not supported yet. Give the permission to the payload folder itself, or use a folder outside the payload folders."));

                if (!seen.Add((permission.Folder ?? "") + "|" + permission.Account))
                    errors.Add(new ValidationError("IK253", $"{label} gives '{permission.Account}' rights on {where} more than once."));
            }
        }

        static void CheckHandler(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label, string executable, string what)
        {
            if (string.IsNullOrWhiteSpace(executable))
                errors.Add(new ValidationError("IK192", $"{label}: the {what} has no program. Pass the executable, or set MainExecutable."));
            else
                CheckInstalledFile(errors, msi, context, label, executable, what);
        }

        static void CheckInstalledFile(List<ValidationError> errors, MsiDefinition msi, InstallerContext context, string label, string? path, string what)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var found = FindInstalledFile(msi, context, path!);
            if (found == null)
                errors.Add(new ValidationError("IK121",
                    $"{label}: the {what} '{path}' is not in any payload. It must be at the top level of a payload's target folder."));
            else if (found.Value.Payload.Feature != null)
                errors.Add(new ValidationError("IK244",
                    $"{label}: the {what} '{path}' is in the optional feature '{found.Value.Payload.Feature}', but it must always be installed. Move it to a payload without a feature."));
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
