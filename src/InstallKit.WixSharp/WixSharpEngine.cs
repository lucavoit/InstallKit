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

            // Registry values (and later the other system settings) live in the install directory's components.
            var installContents = BuildPayloadEntities(msi, context).ToList();
            installContents.AddRange(msi.RegistryEntries.Select(ToRegValue));

            var installDir = new Dir(new Id(InstallDirId), msi.ResolvedInstallDirectory, installContents.ToArray());
            var entities = new List<WixEntity> { installDir };
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
            project.Properties = msi.Properties.Select(p => new Property(p.Key, p.Value)).ToArray();

            // WixSharp needs a ProductId per build; a random one is right for major upgrades.
            project.ProductId = Guid.NewGuid();

            if (!string.IsNullOrWhiteSpace(msi.Manufacturer))
                project.ControlPanelInfo.Manufacturer = msi.Manufacturer;
            if (!string.IsNullOrWhiteSpace(msi.IconFile))
                project.ControlPanelInfo.ProductIcon = Path.GetFullPath(msi.IconFile);
            if (!string.IsNullOrWhiteSpace(msi.LicenseFile))
                project.LicenceFile = Path.GetFullPath(msi.LicenseFile);

            ApplyBranding(project, msi, context, isModern || isWpf);

            if (isModern)
                ModernDialogs.Apply(project, msi);

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

            KeepSource(project, msi.ResolvedOutputFileName, context);

            foreach (var customize in msi.EngineCustomizations.OfType<Action<Project>>())
                customize(project);

            return project.BuildMsi();
        }

        public string BuildBundle(BundleDefinition bundle, IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context)
        {
            ConfigureToolset(context);

            var chain = bundle.Chain.Items.Select(item => ToChainItem(item, builtMsis)).ToArray();

            var wixBundle = new Bundle(bundle.Name, chain)
            {
                UpgradeCode = Guid.Parse(bundle.UpgradeCode!),
                Version = bundle.Version ?? context.Version,
                Manufacturer = bundle.Manufacturer,
                OutDir = context.OutputDirectory,
                OutFileName = bundle.ResolvedOutputFileName,
            };

            if (bundle.Chain.Items.OfType<NetFrameworkChainItem>().Any())
                wixBundle.Include(WixExtension.NetFx);

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

            var output = wixBundle.Build(Path.Combine(context.OutputDirectory, bundle.ResolvedOutputFileName + ".exe"));

            if (context.Signer != null && IOFile.Exists(output))
                SignBundle(output, context);

            return output;
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

        static void ApplyBranding(Project project, MsiDefinition msi, InstallerContext context, bool isModern)
        {
            var branding = msi.Branding ?? new Branding();

            // Classic dialogs keep WiX's stock images unless branding is set; the modern UI is always branded.
            if (branding.IsEmpty && !isModern)
                return;

            var generated = BrandingImages.Generate(msi.Name, branding, context.IntermediateDirectory, msi.ResolvedOutputFileName);
            project.BannerImage = string.IsNullOrWhiteSpace(branding.BannerImage) ? generated.Banner : Path.GetFullPath(branding.BannerImage);
            project.BackgroundImage = string.IsNullOrWhiteSpace(branding.BackgroundImage) ? generated.Background : Path.GetFullPath(branding.BackgroundImage);
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
                        "(.OnAfterInstall(Setup.Configure)) in a project that references InstallKit.Abstractions, which ships the generator."),
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

        static IEnumerable<WixEntity> BuildPayloadEntities(MsiDefinition msi, InstallerContext context)
        {
            // Files that carry a service get their own <File> element; everything else is a wildcard.
            var special = msi.Services
                             .GroupBy(s => Normalize(s.Executable), StringComparer.OrdinalIgnoreCase)
                             .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in msi.Payloads.GroupBy(p => p.TargetSubdirectory ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var items = new List<WixEntity>();

                foreach (var payload in group)
                {
                    var source = InstallerValidator.ResolvePayload(payload, context)!;
                    var specialHere = special
                        .Where(s => IOFile.Exists(Path.Combine(source, Path.GetFileName(s.Key))) &&
                                    string.Equals(Path.GetDirectoryName(s.Key) ?? string.Empty, group.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var entry in specialHere)
                    {
                        var services = entry.Value.Select(ToServiceInstaller).Cast<WixEntity>().ToArray();
                        items.Add(new WixFile(Path.Combine(source, Path.GetFileName(entry.Key)), services));
                        special.Remove(entry.Key);
                    }

                    var excluded = new HashSet<string>(specialHere.Select(s => Path.Combine(source, Path.GetFileName(s.Key))), StringComparer.OrdinalIgnoreCase);
                    items.Add(new Files(Path.Combine(source, "*.*"), path => !excluded.Contains(Path.GetFullPath(path))));
                }

                if (group.Key.Length == 0)
                {
                    foreach (var item in items)
                        yield return item;
                }
                else
                {
                    yield return new Dir(group.Key, items.ToArray());
                }
            }
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
            if (entry.Kind == RegistryValueKind.ExpandString)
                value.Type = "expandable";
            return value;
        }

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
                foreach (var extension in new[] { WixExtension.UI, WixExtension.Util, WixExtension.Bal, WixExtension.NetFx })
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
