using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WixSharp;
using WixSharp.Bootstrapper;
#if !INSTALLKIT_CORE
using Wpf = WixSharp.UI.WPF;
#endif
using IOFile = System.IO.File;
using WixFile = WixSharp.File;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Builds InstallKit definitions with WixSharp v2 (WiX 4 and later). The same source is compiled into
    /// InstallKit.WixSharp (.NET Framework, WixSharp_wix4) and InstallKit.WixSharp.Core (.NET 10, WixSharp.Core,
    /// INSTALLKIT_CORE defined). Keep engine differences inside #if blocks in this file.
    /// </summary>
    public sealed class WixSharpEngine : IInstallerEngine
    {
        const string InstallDirId = "INSTALLDIR";

        /// <summary>Properties every elevated (deferred) handler can read, on top of the ones it lists.</summary>
        const string DeferredBaseProperties = "INSTALLDIR,UILevel,Installed,REMOVE,REINSTALL,UPGRADINGPRODUCTCODE,WIX_UPGRADE_DETECTED";

#if INSTALLKIT_CORE
        public string Name => "WixSharp.Core";

        // Install events: generated [CustomAction] entry points, compiled to native code by WixSharp.Core.
        // The WPF dialogs would need a separate .NET Framework UI project, so ModernWpf falls back to Modern.
        public InstallerCapabilities Capabilities =>
            InstallerCapabilities.InstallEvents | InstallerCapabilities.ModernUi | InstallerCapabilities.BrandedImages;
#else
        public string Name => "WixSharp";

        public InstallerCapabilities Capabilities =>
            InstallerCapabilities.InstallEvents | InstallerCapabilities.ModernUi | InstallerCapabilities.BrandedImages | InstallerCapabilities.WpfUi;
#endif

        public string BuildMsi(MsiDefinition msi, InstallerContext context)
        {
            ConfigureToolset(context);
            return CreateMsiProject(msi, context).BuildMsi();
        }

        /// <summary>
        /// Maps the model to a WixSharp project: everything BuildMsi does except picking the WiX tools and running them.
        /// Separate so tests can generate the WiX source of a model without WiX.
        /// </summary>
        internal static Project CreateMsiProject(MsiDefinition msi, InstallerContext context)
        {
            // Environment variables live in the install directory. WixSharp only accepts registry values at the
            // top level of the project, where it creates a component for each one.
            var installContents = BuildPayloadEntities(msi, context).ToList();
            installContents.AddRange(msi.EnvironmentVariables.Select((v, i) => ToEnvironmentVariable(v, i, msi.Scope)));
            installContents.AddRange(msi.FirewallRules.Select(ToFirewallException));

            var installDir = BuildFolder(msi.ResolvedInstallDirectory, installContents.ToArray(), PermissionsFor(msi, null), new Id(InstallDirId));
            if (msi.Features.Count > 0 && msi.Ui != InstallerUi.FeatureTree && msi.Ui != InstallerUi.Modern && msi.Ui != InstallerUi.ModernInstallDir)
                Console.WriteLine($"InstallKit : warning IK246: MSI '{msi.Name}' has optional features, which users can only choose in the FeatureTree and Modern UIs or with ADDLOCAL and REMOVE on the command line. Set Ui = InstallerUi.Modern or InstallerUi.FeatureTree to show them.");
            var entities = new List<WixEntity> { installDir };
            entities.AddRange(msi.ResolvedRegistryEntries.Select(ToRegValue));
            entities.AddRange(msi.CloseApplications.Select(ToCloseApplication));
            entities.AddRange(BuildShortcutDirs(msi));
            entities.AddRange(msi.Commands.Select(ToAction));

            var installActions = BuildInstallActions(msi).ToList();
            entities.AddRange(installActions.Select(a => a.Action));

            var isModern = msi.Ui == InstallerUi.Modern || msi.Ui == InstallerUi.ModernInstallDir;
            var isWpf = msi.Ui == InstallerUi.ModernWpf;

#if INSTALLKIT_CORE
            var project = new Project(msi.Name, entities.ToArray());
#else
            // ManagedProject (with its embedded UI host) only for the WPF dialogs.
            var project = isWpf
                ? new ManagedProject(msi.Name, entities.ToArray())
                : new Project(msi.Name, entities.ToArray());
#endif

            project.UpgradeCode = Guid.Parse(msi.UpgradeCode!);
            project.Version = msi.Version ?? context.Version;
            project.Description = msi.Description ?? msi.Name;
            project.Platform = ToPlatform(msi.Platform);
            project.Scope = msi.Scope == InstallKit.InstallScope.PerUser ? global::WixSharp.InstallScope.perUser : global::WixSharp.InstallScope.perMachine;
            project.MajorUpgrade = MajorUpgrade.Default;
            // The native modern dialogs replace WixUI_Minimal's UIRef (see ModernDialogs).
            project.UI = isModern ? WUI.WixUI_Minimal : ToUi(msi.Ui);
            project.OutDir = context.OutputDirectory;
            project.OutFileName = msi.ResolvedOutputFileName;
            // A property that the user can set in a dialog has to be secure, or the elevated install would ignore the value.
            project.Properties = msi.Properties.Select(p => new Property(p.Key, p.Value))
                .Concat(msi.PromptedProperties.Select(ToPromptedProperty)).ToArray();
            if (msi.PromptedProperties.Count > 0 && msi.Ui != InstallerUi.Modern && msi.Ui != InstallerUi.ModernInstallDir)
                Console.WriteLine($"InstallKit : warning IK285: MSI '{msi.Name}' has prompted properties, which only the modern UIs ask for. With {msi.Ui} they keep their defaults unless given on the command line.");

            // WixSharp needs a ProductId per build; a random one is right for major upgrades.
            project.ProductId = Guid.NewGuid();

            if (!string.IsNullOrWhiteSpace(msi.Manufacturer))
                project.ControlPanelInfo.Manufacturer = msi.Manufacturer;
            if (!string.IsNullOrWhiteSpace(msi.IconFile))
                project.ControlPanelInfo.ProductIcon = Path.GetFullPath(msi.IconFile);
            if (!string.IsNullOrWhiteSpace(msi.LicenseFile))
                project.LicenceFile = Path.GetFullPath(msi.LicenseFile);

            var hero = ApplyBranding(project, msi, context, isModern || isWpf);

            if (isModern)
                ModernDialogs.Apply(project, msi, hero);

#if !INSTALLKIT_CORE
            if (project is ManagedProject managed)
                managed.ManagedUI = BuildWpfUi(msi);
            else if (msi.EngineCustomizations.OfType<WpfDialogInsertion>().Any())
                Console.WriteLine($"InstallKit : warning IK202: MSI '{msi.Name}' adds WPF dialogs, which only show with Ui = ModernWpf.");
#endif

            if (installActions.Count > 0)
            {
                // The generated entry points live in the installer program; WixSharp embeds it (.NET Framework)
                // or compiles it to native code (WixSharp.Core). It also needs InstallKit.Abstractions.
                Compiler.ClientAssembly = installActions[0].Assembly;
                foreach (var reference in new[] { typeof(InstallEvent).Assembly.Location })
                {
                    if (!project.DefaultRefAssemblies.Contains(reference))
                        project.DefaultRefAssemblies.Add(reference);
                }
            }

            if (msi.FirewallRules.Count > 0)
                project.Include(WixExtension.Fire);
            if (msi.CloseApplications.Count > 0)
                project.Include(WixExtension.Util);

            // Before the source is kept, so the kept file is the one WiX compiles.
            project.WixSourceGenerated += document => IdentifierLimits.Apply(document);

            KeepSource(project, msi.ResolvedOutputFileName, context);

            foreach (var customize in msi.EngineCustomizations.OfType<Action<Project>>())
                customize(project);

            return project;
        }

        public string BuildBundle(BundleDefinition bundle, IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context)
        {
            ConfigureToolset(context);

            var wixBundle = CreateBundleProject(bundle, builtMsis, context);

            var output = wixBundle.Build(Path.Combine(context.OutputDirectory, bundle.ResolvedOutputFileName + ".exe"));

            if (context.Signer != null && IOFile.Exists(output))
                SignBundle(output, context);

            return output;
        }

        /// <summary>The bundle counterpart of <see cref="CreateMsiProject"/>: everything except running WiX and signing.</summary>
        internal static Bundle CreateBundleProject(BundleDefinition bundle, IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context)
        {
            var chain = bundle.Chain.Items.Select(item => ToChainItem(item, builtMsis)).ToArray();

            var wixBundle = new Bundle(bundle.Name, chain)
            {
                UpgradeCode = Guid.Parse(bundle.UpgradeCode!),
                Version = bundle.Version ?? context.Version,
                Manufacturer = bundle.Manufacturer,
                OutDir = context.OutputDirectory,
                OutFileName = bundle.ResolvedOutputFileName,
            };

            if (bundle.Chain.Items.OfType<NetFrameworkChainItem>().Any() || bundle.Chain.Searches.OfType<DotNetBundleSearch>().Any())
                wixBundle.Include(WixExtension.NetFx);

            AddSearches(wixBundle, bundle.Chain.Searches);

            wixBundle.Application = new LicenseBootstrapperApplication
            {
                // An .rtf is shown in the dialog; a URL becomes a license link; null hides the license.
                LicensePath = bundle.License == null || IsUrl(bundle.License) ? bundle.License : Path.GetFullPath(bundle.License),
            };

            if (!string.IsNullOrWhiteSpace(bundle.IconFile))
                wixBundle.IconFile = Path.GetFullPath(bundle.IconFile);

            var bundleLogo = BrandingImages.LogoAsPng(bundle.Branding, context.IntermediateDirectory, bundle.ResolvedOutputFileName);
            if (bundleLogo != null)
                wixBundle.Application.LogoFile = bundleLogo;

            KeepSource(wixBundle, bundle.ResolvedOutputFileName, context);

            foreach (var customize in bundle.EngineCustomizations.OfType<Action<Bundle>>())
                customize(wixBundle);

            return wixBundle;
        }

        /// <summary>Adds the lookups that prerequisite helpers need: registry values and installed .NET runtimes.</summary>
        static void AddSearches(Bundle bundle, IReadOnlyList<BundleSearch> searches)
        {
            var registry = searches.OfType<RegistryBundleSearch>().ToList();
            if (registry.Count > 0)
            {
                bundle.Include(WixExtension.Util);
                bundle.WixSourceGenerated += document =>
                {
                    System.Xml.Linq.XNamespace util = "http://wixtoolset.org/schemas/v4/wxs/util";
                    var root = document.Descendants().First(e => e.Name.LocalName == "Bundle");
                    for (var i = 0; i < registry.Count; i++)
                    {
                        var search = registry[i];
                        root.Add(new System.Xml.Linq.XElement(util + "RegistrySearch",
                            new System.Xml.Linq.XAttribute("Id", $"InstallKitSearch{i + 1}"),
                            new System.Xml.Linq.XAttribute("Variable", search.Variable),
                            new System.Xml.Linq.XAttribute("Root", search.Root == InstallKit.RegistryHive.CurrentUser ? "HKCU" : "HKLM"),
                            new System.Xml.Linq.XAttribute("Key", search.Key),
                            new System.Xml.Linq.XAttribute("Value", search.Value),
                            new System.Xml.Linq.XAttribute("Result", "value"),
                            new System.Xml.Linq.XAttribute("Bitness", search.Win64 ? "always64" : "always32")));
                    }
                };
            }

            foreach (var search in searches.OfType<DotNetBundleSearch>())
            {
                bundle.GenericItems.Add(new DotNetCoreSearch
                {
                    Variable = search.Variable,
                    RuntimeType = search.Kind == DotNetRuntimeKind.AspNetCore ? "aspnet" : search.Kind == DotNetRuntimeKind.WindowsDesktop ? "desktop" : "core",
                    MajorVersion = search.MajorVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Platform = search.Architecture == PrerequisiteArchitecture.X86 ? "x86" : search.Architecture == PrerequisiteArchitecture.Arm64 ? "arm64" : "x64",
                });
            }
        }

        /// <summary>
        /// Burn bundles carry their own engine. Signing only the .exe leaves the engine unsigned, so:
        /// detach the engine, sign it, reattach it, then sign the bundle.
        /// </summary>
        static void SignBundle(string bundlePath, InstallerContext context)
        {
            var engine = Path.Combine(context.IntermediateDirectory, Path.GetFileNameWithoutExtension(bundlePath) + ".engine.exe");

            RunWix(context, $"burn detach \"{bundlePath}\" -engine \"{engine}\"");
            context.Signer!.Sign(engine);
            RunWix(context, $"burn reattach \"{bundlePath}\" -engine \"{engine}\" -o \"{bundlePath}\"");
            context.Signer.Sign(bundlePath);
        }

        static void RunWix(InstallerContext context, string arguments)
        {
            // With a pinned local tool (the SDK's default) run it through `dotnet`, otherwise expect wix on PATH.
            var useLocalTool = !string.IsNullOrWhiteSpace(context.WixVersion);
            if (!string.IsNullOrWhiteSpace(context.AcceptedWixEula))
                arguments += " -acceptEula " + context.AcceptedWixEula;
            var start = new System.Diagnostics.ProcessStartInfo(useLocalTool ? "dotnet" : "wix", useLocalTool ? "wix " + arguments : arguments)
            {
                UseShellExecute = false,
            };

            using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Could not start wix.");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"'wix {arguments}' failed with exit code {process.ExitCode}.");
        }

        /// <summary>Sets the dialog images; returns the full-page background of the modern pages, or <c>null</c>.</summary>
        static string? ApplyBranding(Project project, MsiDefinition msi, InstallerContext context, bool isModern)
        {
            var branding = msi.Branding ?? new Branding();

            // Classic dialogs keep WiX's stock images unless branding is set; the modern UI is always branded.
            if (branding.IsEmpty && !isModern)
                return null;

            var generated = BrandingImages.Generate(msi.Name, branding, context.IntermediateDirectory, msi.ResolvedOutputFileName);
            project.BannerImage = string.IsNullOrWhiteSpace(branding.BannerImage) ? generated.Banner : Path.GetFullPath(branding.BannerImage);
            project.BackgroundImage = string.IsNullOrWhiteSpace(branding.BackgroundImage) ? generated.Background : Path.GetFullPath(branding.BackgroundImage);
            return string.IsNullOrWhiteSpace(branding.BackgroundImage) ? generated.Page : null;
        }

#if !INSTALLKIT_CORE
        /// <summary>The compact WPF dialog set: welcome, (license), progress, finish; repair/remove.</summary>
        static IManagedUI BuildWpfUi(MsiDefinition msi)
        {
            var pages = new List<(ModernDialog Page, Type Type)> { (ModernDialog.Welcome, typeof(Wpf.WelcomeDialog)) };
            if (!string.IsNullOrWhiteSpace(msi.LicenseFile))
                pages.Add((ModernDialog.License, typeof(Wpf.LicenceDialog)));
            pages.Add((ModernDialog.Progress, typeof(Wpf.ProgressDialog)));
            pages.Add((ModernDialog.Finish, typeof(Wpf.ExitDialog)));

            var install = new ManagedDialogs();
            foreach (var (page, type) in pages)
            {
                install.Add(type);
                foreach (var custom in msi.EngineCustomizations.OfType<WpfDialogInsertion>().Where(c => c.After == page))
                    install.Add(custom.DialogType);
            }

            // Custom dialogs anchored to a page that is not shown go right after the welcome page.
            var shown = new HashSet<ModernDialog>(pages.Select(p => p.Page));
            var orphans = msi.EngineCustomizations.OfType<WpfDialogInsertion>().Where(c => !shown.Contains(c.After)).Select(c => c.DialogType);
            install.InsertRange(1, orphans);

            var modify = new ManagedDialogs();
            modify.Add(typeof(Wpf.MaintenanceTypeDialog));
            modify.Add(typeof(Wpf.ProgressDialog));
            modify.Add(typeof(Wpf.ExitDialog));

            return new ManagedUI { InstallDialogs = install, ModifyDialogs = modify };
        }

#endif

        /// <summary>
        /// One custom action per install event handler, pointing at the entry point the InstallKit source
        /// generator emitted (InstallKit.Generated.InstallKitActions). No reflection at install time.
        /// </summary>
        static IEnumerable<(WixEntity Action, string Assembly)> BuildInstallActions(MsiDefinition msi)
        {
            foreach (var handler in msi.EventHandlers)
            {
                var generated = GeneratedInstallActions.Find(handler) ?? throw new InstallerValidationException(new[]
                {
                    new ValidationError("IK163",
                        $"MSI '{msi.Name}': no generated custom action for the {handler.Stage} handler " +
                        $"'{GeneratedInstallActions.KeyOf(handler.Handler.Method)}'. Register it as a static method " +
                        "(.Lifecycle(l => l.OnAfterInstall(Setup.Configure))) in a project that references InstallKit.Abstractions, which ships the generator."),
                });

                var elevated = handler.Stage == InstallStage.AfterInstall;
                ManagedAction action = elevated ? new ElevatedManagedAction(generated.ActionName) : new ManagedAction(generated.ActionName);

                action.ActionAssembly = generated.Assembly.Location;
                action.Return = Return.check;
                action.Condition = Condition.Always;

                switch (handler.Stage)
                {
                    case InstallStage.Load:
                        action.When = When.Before;
                        action.Step = Step.AppSearch;
                        break;
                    case InstallStage.BeforeInstall:
                        action.When = When.Before;
                        action.Step = Step.InstallFiles;
                        break;
                    default:
                        action.When = When.After;
                        action.Step = Step.InstallFiles;
                        action.UsesProperties = string.Join(",", DeferredBaseProperties.Split(',').Concat(handler.UsesProperties).Distinct());
                        break;
                }

                yield return (action, generated.Assembly.Location);
            }
        }

        static WixEntity ToAction(RunCommand command)
        {
            var path = command.Path.Contains("[") || Path.IsPathRooted(command.Path)
                ? command.Path
                : $"[{InstallDirId}]{Normalize(command.Path)}";

            var afterInstall = command.Timing == RunCommandTiming.AfterInstall;

            return new PathFileAction(
                path,
                command.Arguments ?? string.Empty,
                InstallDirId,
                command.CheckExitCode ? Return.check : Return.ignore,
                When.Before,
                afterInstall ? Step.InstallFinalize : Step.RemoveFiles,
                afterInstall ? Condition.Create("NOT (REMOVE=\"ALL\")") : Condition.BeingUninstalledAndNotBeingUpgraded)
            {
                Execute = Execute.deferred,
                Impersonate = !command.Elevated,
            };
        }

        static Feature ToFeature(OptionalFeature feature) =>
            new Feature(feature.Name, feature.Description ?? string.Empty, feature.InstalledByDefault, true) { Id = feature.Id };

        // Installed paths use backslashes whatever the build machine is, which Path.GetFileName and GetDirectoryName do not split on Linux.
        static string InstalledFileName(string path) => path.Substring(path.LastIndexOf('\\') + 1);

        static string InstalledFolder(string path)
        {
            var cut = path.LastIndexOf('\\');
            return cut <= 0 ? string.Empty : path.Substring(0, cut);
        }

        static void AddChild(Dictionary<string, List<WixEntity>> children, string file, WixEntity child)
        {
            var key = Normalize(file);
            if (!children.TryGetValue(key, out var list))
                children[key] = list = new List<WixEntity>();
            list.Add(child);
        }

        static WixEntity ToXmlFile(XmlValue edit, int index) =>
            // The file is the one the element is nested in; the XPath's brackets must be escaped for MSI.
            new XmlFile(new Id($"InstallKitXml{index + 1}"), XmlFileAction.setValue, edit.EscapedXPath, edit.Value);

        static IEnumerable<WixEntity> BuildPayloadEntities(MsiDefinition msi, InstallerContext context)
        {
            var features = msi.Features.ToDictionary(f => f.Id, ToFeature, StringComparer.OrdinalIgnoreCase);

            // Files that carry a service or an XML edit get their own <File> element; everything else is a wildcard.
            var special = new Dictionary<string, List<WixEntity>>(StringComparer.OrdinalIgnoreCase);
            foreach (var service in msi.Services)
                AddChild(special, service.Executable, ToServiceInstaller(service));
            for (var i = 0; i < msi.XmlValues.Count; i++)
                AddChild(special, msi.XmlValues[i].File, ToXmlFile(msi.XmlValues[i], i));

            foreach (var group in msi.Payloads.GroupBy(p => p.TargetSubdirectory ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var items = new List<WixEntity>();

                foreach (var payload in group)
                {
                    var source = InstallerValidator.ResolvePayload(payload, context)!;
                    var specialHere = special
                        .Where(s => IOFile.Exists(Path.Combine(source, InstalledFileName(s.Key))) &&
                                    string.Equals(InstalledFolder(s.Key), group.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var entry in specialHere)
                    {
                        var file = new WixFile(Path.Combine(source, InstalledFileName(entry.Key)), entry.Value.ToArray());
                        // The file carries a service or an XML edit, so it needs its feature itself: only the wildcard below sets it otherwise.
                        if (payload.Feature != null)
                            file.Feature = features[new OptionalFeature(payload.Feature).Id];
                        items.Add(file);
                        special.Remove(entry.Key);
                    }

                    var excluded = new HashSet<string>(specialHere.Select(s => Path.Combine(source, InstalledFileName(s.Key))), StringComparer.OrdinalIgnoreCase);
                    var files = new Files(Path.Combine(source, "*.*"), path => !excluded.Contains(Path.GetFullPath(path)));
                    if (payload.Feature != null)
                        files.Feature = features[new OptionalFeature(payload.Feature).Id];
                    items.Add(files);
                }

                if (group.Key.Length == 0)
                {
                    foreach (var item in items)
                        yield return item;
                }
                else
                {
                    yield return BuildFolder(group.Key, items.ToArray(), PermissionsFor(msi, group.Key));
                }
            }

            // A folder that only has permissions (a logs folder, for example) is created for them.
            var payloadFolders = new HashSet<string>(msi.Payloads.Select(p => p.TargetSubdirectory ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            foreach (var folder in msi.FolderPermissions.Where(p => p.Folder != null).Select(p => p.Folder!).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!payloadFolders.Contains(folder))
                    yield return BuildFolder(folder, new WixEntity[0], PermissionsFor(msi, folder));
            }
        }

        static DirPermission[] PermissionsFor(MsiDefinition msi, string? folder) =>
            msi.FolderPermissions
                .Where(p => string.Equals(p.Folder, folder, StringComparison.OrdinalIgnoreCase))
                .Select(ToDirPermission)
                .ToArray();

        static DirPermission ToDirPermission(FolderPermission permission)
        {
            var result = new DirPermission(permission.UserName) { Domain = permission.Domain };
            switch (permission.Rights)
            {
                case FolderRights.Read:
                    result.GenericRead = true;
                    result.GenericExecute = true;
                    break;
                case FolderRights.Modify:
                    result.GenericRead = true;
                    result.GenericExecute = true;
                    result.GenericWrite = true;
                    result.Delete = true;
                    break;
                default:
                    result.GenericAll = true;
                    break;
            }

            return result;
        }

        /// <summary>
        /// A folder with its contents. WixSharp puts the permissions of a multi-segment path on the first segment
        /// (for the install directory that would be Program Files itself), so a folder with permissions is built as
        /// nested single-segment folders and the permissions go on the last one.
        /// </summary>
        static Dir BuildFolder(string path, WixEntity[] contents, DirPermission[] permissions, Id? id = null)
        {
            // Compare as object: WixSharp's Id overloads != and throws when the other side is null.
            Dir Leaf(string name) => (object?)id != null ? new Dir(id!, name, contents) : new Dir(name, contents);

            if (permissions.Length == 0)
                return Leaf(path);

            var split = path.LastIndexOfAny(new[] { '\\', '/' });
            if (split < 0)
            {
                var single = Leaf(path);
                single.Permissions = permissions;
                return single;
            }

            var leaf = Leaf(path.Substring(split + 1));
            leaf.Permissions = permissions;
            return new Dir(path.Substring(0, split), leaf);
        }

        static IEnumerable<WixEntity> BuildShortcutDirs(MsiDefinition msi)
        {
            if (string.IsNullOrWhiteSpace(msi.MainExecutable))
                yield break;

            var target = $"[{InstallDirId}]{Normalize(msi.MainExecutable!)}";

            if (msi.Shortcuts.StartMenu)
            {
                var menu = string.IsNullOrWhiteSpace(msi.Manufacturer) ? $@"%ProgramMenu%\{msi.Name}" : $@"%ProgramMenu%\{msi.Manufacturer}\{msi.Name}";
                yield return new Dir(menu,
                    new ExeFileShortcut(msi.Name, target, arguments: string.Empty) { WorkingDirectory = InstallDirId },
                    new ExeFileShortcut($"Uninstall {msi.Name}", "[System64Folder]msiexec.exe", "/x [ProductCode]"));
            }

            if (msi.Shortcuts.Desktop)
            {
                yield return new Dir("%Desktop%",
                    new ExeFileShortcut(msi.Name, target, arguments: string.Empty) { WorkingDirectory = InstallDirId });
            }
        }

        static WixEntity ToRegValue(RegistryEntry entry, int index)
        {
            var hive = entry.Hive switch
            {
                InstallKit.RegistryHive.CurrentUser => global::WixSharp.RegistryHive.CurrentUser,
                InstallKit.RegistryHive.ClassesRoot => global::WixSharp.RegistryHive.ClassesRoot,
                InstallKit.RegistryHive.Users => global::WixSharp.RegistryHive.Users,
                _ => global::WixSharp.RegistryHive.LocalMachine,
            };

            // A null name is the key's default value, which WixSharp spells as an empty name.
            // Every value needs its own id: two default values would otherwise both be called "RegValue".
            var value = new RegValue(new Id($"InstallKitReg{index + 1}"), hive, entry.Key, entry.Name ?? string.Empty,
                entry.Kind == RegistryValueKind.DWord ? (object)entry.DWordValue!.Value : entry.Value);
            // WixSharp turns any value that contains '%' (such as "%1" in an open command) into REG_EXPAND_SZ
            // on its own, so say what is meant.
            if (entry.Kind != RegistryValueKind.DWord)
                value.Type = entry.Kind == RegistryValueKind.ExpandString ? "expandable" : "string";
            return value;
        }

        static WixEntity ToEnvironmentVariable(EnvironmentVariableEntry variable, int index, InstallKit.InstallScope scope) =>
            new global::WixSharp.EnvironmentVariable(new Id($"InstallKitEnv{index + 1}"), variable.Name, variable.Value)
            {
                Action = EnvVarAction.set,
                Part = variable.Action switch
                {
                    EnvironmentVariableAction.Append => EnvVarPart.last,
                    EnvironmentVariableAction.Prepend => EnvVarPart.first,
                    _ => EnvVarPart.all,
                },
                System = scope == InstallKit.InstallScope.PerMachine,
                Permanent = false,
            };

        static Property ToPromptedProperty(PromptedProperty prompt)
        {
            // WiX rejects an empty Value, so a prompt without a default declares the property without one.
            var property = new Property(prompt.Name, prompt.DefaultValue ?? "x") { Secure = true };
            if (prompt.DefaultValue == null)
                property.Value = null;
            return property;
        }

        static WixEntity ToCloseApplication(CloseApplicationRule rule, int index) =>
            new global::WixSharp.CloseApplication(new Id($"InstallKitClose{index + 1}"), rule.ProcessName, closeMessage: true, rebootPrompt: false)
            {
                Timeout = rule.TimeoutSeconds,
                // The exit code the process is ended with; null leaves a program that ignored the request running.
                TerminateProcess = rule.Terminate ? 0 : (int?)null,
                // Ask again in the elevated phase, so programs started by other users or services are asked too.
                ElevatedCloseMessage = true,
            };

        static WixEntity ToFirewallException(FirewallRule rule, int index) =>
            new FirewallException(new Id($"InstallKitFirewall{index + 1}"), rule.Name)
            {
                // WixSharp leaves a rule disabled unless it is asked to enable it.
                Enabled = true,
                Port = rule.Port,
                Protocol = rule.Protocol switch
                {
                    FirewallProtocol.Tcp => FirewallExceptionProtocol.tcp,
                    FirewallProtocol.Udp => FirewallExceptionProtocol.udp,
                    _ => (FirewallExceptionProtocol?)null,
                },
                Program = rule.Executable == null ? null : $"[{InstallDirId}]{Normalize(rule.Executable)}",
                Scope = rule.Scope == FirewallScope.LocalSubnet ? FirewallExceptionScope.localSubnet : FirewallExceptionScope.any,
                Profile = rule.Profile switch
                {
                    FirewallProfile.Domain => FirewallExceptionProfile.domain,
                    FirewallProfile.Private => FirewallExceptionProfile.@private,
                    FirewallProfile.Public => FirewallExceptionProfile.@public,
                    _ => FirewallExceptionProfile.all,
                },
            };

        static ServiceInstaller ToServiceInstaller(ServiceDefinition service) => new ServiceInstaller
        {
            Name = service.Name,
            DisplayName = service.DisplayName ?? service.Name,
            Description = service.Description ?? service.DisplayName ?? service.Name,
            Start = service.StartMode switch
            {
                ServiceStartMode.Manual => SvcStartType.demand,
                ServiceStartMode.Disabled => SvcStartType.disabled,
                _ => SvcStartType.auto,
            },
            Account = service.Account,
            Arguments = service.Arguments,
            StartOn = service.StartOnInstall ? SvcEvent.Install : null,
            StopOn = SvcEvent.InstallUninstall_Wait,
            RemoveOn = SvcEvent.Uninstall_Wait,
        };

        static global::WixSharp.Bootstrapper.ChainItem ToChainItem(InstallKit.ChainItem item, IReadOnlyDictionary<MsiDefinition, string> builtMsis)
        {
            global::WixSharp.Bootstrapper.ChainItem result;

            switch (item)
            {
                case NetFrameworkChainItem netfx:
                    result = new PackageGroupRef(netfx.Version switch
                    {
                        NetFrameworkVersion.NetFx462 => "NetFx462Web",
                        NetFrameworkVersion.NetFx472 => "NetFx472Web",
                        NetFrameworkVersion.NetFx48 => "NetFx48Web",
                        _ => "NetFx481Web",
                    });
                    break;

                case MsiChainItem msi:
                    var path = msi.Definition != null ? builtMsis[msi.Definition] : Path.GetFullPath(msi.Path!);
                    var package = new MsiPackage(path)
                    {
                        DisplayInternalUI = msi.DisplayInternalUi,
                        Compressed = true,
                        InstallCondition = msi.InstallCondition,
                    };
                    if (msi.Properties.Count > 0)
                        package.MsiProperties = string.Join(";", msi.Properties.Select(p => $"{p.Key}={p.Value}"));
                    result = package;
                    break;

                case ExeChainItem exe when exe.Download != null:
                    // Nothing is embedded: Burn downloads the file and checks its hash and size before running it.
                    var download = exe.Download;
                    // WiX rejects a payload element on a package that also has SourceFile, Name, DownloadUrl or Compressed,
                    // so the file name, address, hash and size all live on the payload.
                    result = new ExePackage
                    {
                        InstallArguments = exe.InstallArguments,
                        UninstallArguments = exe.UninstallArguments,
                        DetectCondition = exe.DetectCondition,
                        PerMachine = exe.PerMachine,
                        Permanent = exe.Permanent,
                        InstallCondition = exe.InstallCondition,
                        Payloads = new Payload[]
                        {
                            new ExePackagePayload
                            {
                                Name = download.ResolvedFileName,
                                DownloadUrl = download.Url,
                                Hash = download.Sha512!.ToUpperInvariant(),
                                Size = (int)download.Size,
                            },
                        },
                    };
                    break;

                case ExeChainItem exe:
                    result = new ExePackage(Path.GetFullPath(exe.Path))
                    {
                        InstallArguments = exe.InstallArguments,
                        UninstallArguments = exe.UninstallArguments,
                        DetectCondition = exe.DetectCondition,
                        PerMachine = exe.PerMachine,
                        Permanent = exe.Permanent,
                        Compressed = true,
                        InstallCondition = exe.InstallCondition,
                    };
                    break;

                default:
                    throw new NotSupportedException($"The WixSharp engine does not support chain items of type {item.GetType().Name}.");
            }

            // WiX rejects Vital on a PackageGroupRef (the .NET Framework prerequisite); packages accept it.
            if (!(result is PackageGroupRef))
                result.Vital = item.Vital;
            return result;
        }

        static void ConfigureToolset(InstallerContext context)
        {
            // WiX extensions must match the major version of wix.exe; pin them to the tool version
            // from .config/dotnet-tools.json so builds don't pick up whatever is newest on NuGet.
            if (!string.IsNullOrWhiteSpace(context.WixVersion))
            {
                foreach (var extension in new[] { WixExtension.UI, WixExtension.Util, WixExtension.Bal, WixExtension.NetFx, WixExtension.Fire })
                    extension.PreferredVersion = context.WixVersion;
            }

            // WiX 7+ needs an explicit EULA acceptance; WixSharp passes it to wix.exe as -acceptEula.
            global::WixSharp.CommonTasks.WixTools.AcceptEulaFor = context.AcceptedWixEula ?? string.Empty;
        }

        static void KeepSource(WixProject project, string name, InstallerContext context)
        {
            if (!context.EmitSource)
                return;

            project.WixSourceGenerated += document =>
            {
                Directory.CreateDirectory(context.IntermediateDirectory);
                document.Save(Path.Combine(context.IntermediateDirectory, name + ".wxs"));
            };
        }

        static Platform ToPlatform(InstallerPlatform platform) => platform switch
        {
            InstallerPlatform.X86 => Platform.x86,
            InstallerPlatform.Arm64 => Platform.arm64,
            _ => Platform.x64,
        };

        static WUI ToUi(InstallerUi ui) => ui switch
        {
            InstallerUi.ProgressOnly => WUI.WixUI_ProgressOnly,
            InstallerUi.InstallDir => WUI.WixUI_InstallDir,
            InstallerUi.FeatureTree => WUI.WixUI_FeatureTree,
            _ => WUI.WixUI_Minimal,
        };

        static string Normalize(string path) => path.Replace('/', '\\').TrimStart('\\');

        static bool IsUrl(string value) =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
