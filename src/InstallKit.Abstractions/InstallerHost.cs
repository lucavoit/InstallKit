using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InstallKit
{
    /// <summary>
    /// Entry point of an installer project:
    /// <code>
    /// return InstallerHost.Run(args, plan =&gt; plan.Msi("My App", msi =&gt; { ... }));
    /// </code>
    /// </summary>
    public static class InstallerHost
    {
        /// <summary>Validates and builds the plan. Returns a process exit code.</summary>
        /// <param name="args">Command-line arguments (see <see cref="InstallerContext.FromEnvironment"/>).</param>
        /// <param name="configure">Defines the MSIs and bundles.</param>
        /// <param name="engine">Optional engine. Defaults to the one named by <see cref="InstallerEngineAttribute"/>.</param>
        public static int Run(string[] args, Action<InstallerPlan> configure, IInstallerEngine? engine = null)
        {
            try
            {
                var context = InstallerContext.FromEnvironment(args);
                var plan = new InstallerPlan(context);
                configure(plan);

                var outputs = Build(plan, engine ?? ResolveEngine(Assembly.GetEntryAssembly()));

                if (context.OutputsFile != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(context.OutputsFile))!);
                    File.WriteAllLines(context.OutputsFile, outputs);
                }

                foreach (var output in outputs)
                    Console.WriteLine($"InstallKit: built {output}");

                return 0;
            }
            catch (InstallerValidationException ex)
            {
                foreach (var error in ex.Errors)
                    Console.Error.WriteLine(error);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"InstallKit : error IK900: {ex.Message}");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        /// <summary>Validates the plan, builds every MSI and then every bundle, and returns the output paths.</summary>
        public static IReadOnlyList<string> Build(InstallerPlan plan, IInstallerEngine engine)
        {
            ApplyCapabilities(plan, engine);

            var errors = InstallerValidator.Validate(plan);
            if (errors.Count > 0)
                throw new InstallerValidationException(errors);

            Directory.CreateDirectory(plan.Context.OutputDirectory);
            Directory.CreateDirectory(plan.Context.IntermediateDirectory);

            var outputs = new List<string>();
            var builtMsis = new Dictionary<MsiDefinition, string>();

            foreach (var msi in plan.Msis)
            {
                var path = engine.BuildMsi(msi, plan.Context);
                EnsureBuilt(path, $"MSI '{msi.Name}'", engine);
                plan.Context.Signer?.Sign(path); // before any bundle embeds it
                builtMsis[msi] = path;
                outputs.Add(path);
            }

            // Bundles are signed by the engine: the bootstrapper engine inside the bundle has to be signed
            // before the bundle itself, which only the engine knows how to do.
            foreach (var bundle in plan.Bundles)
            {
                var path = engine.BuildBundle(bundle, builtMsis, plan.Context);
                EnsureBuilt(path, $"bundle '{bundle.Name}'", engine);
                outputs.Add(path);
            }

            return outputs;
        }

        /// <summary>
        /// Adapts the plan to what the engine supports: the modern UI falls back to the classic dialogs
        /// with a warning; install event handlers without engine support are an error.
        /// </summary>
        static void ApplyCapabilities(InstallerPlan plan, IInstallerEngine engine)
        {
            var errors = new List<ValidationError>();

            foreach (var msi in plan.Msis)
            {
                if (msi.Ui == InstallerUi.ModernWpf && !engine.Capabilities.HasFlag(InstallerCapabilities.WpfUi))
                {
                    Console.WriteLine($"InstallKit : warning IK200: the {engine.Name} engine has no WPF dialogs; MSI '{msi.Name}' uses the native modern dialogs.");
                    msi.Ui = InstallerUi.Modern;
                }

                if ((msi.Ui == InstallerUi.Modern || msi.Ui == InstallerUi.ModernInstallDir) && !engine.Capabilities.HasFlag(InstallerCapabilities.ModernUi))
                {
                    Console.WriteLine($"InstallKit : warning IK200: the {engine.Name} engine has no modern UI; MSI '{msi.Name}' uses the classic dialogs.");
                    msi.Ui = msi.Ui == InstallerUi.ModernInstallDir ? InstallerUi.InstallDir : InstallerUi.Minimal;
                }

                if (msi.EventHandlers.Count > 0 && !engine.Capabilities.HasFlag(InstallerCapabilities.InstallEvents))
                    errors.Add(new ValidationError("IK201", $"MSI '{msi.Name}' has install event handlers, which the {engine.Name} engine does not support."));
            }

            if (errors.Count > 0)
                throw new InstallerValidationException(errors);
        }

        /// <summary>Creates the engine named by the assembly's <see cref="InstallerEngineAttribute"/>.</summary>
        public static IInstallerEngine ResolveEngine(Assembly? assembly)
        {
            var attribute = assembly?.GetCustomAttributes(typeof(InstallerEngineAttribute), false)
                                     .OfType<InstallerEngineAttribute>()
                                     .FirstOrDefault();

            if (attribute == null)
                throw new InvalidOperationException(
                    "No installer engine is configured. Use the InstallKit SDK (it sets one from the InstallKitEngine property) " +
                    "or pass an engine to InstallerHost.Run.");

            var type = Type.GetType(attribute.EngineTypeName, throwOnError: false)
                       ?? throw new InvalidOperationException($"Installer engine type '{attribute.EngineTypeName}' could not be loaded. Is its package referenced?");

            return (IInstallerEngine)Activator.CreateInstance(type)!;
        }

        static void EnsureBuilt(string? path, string what, IInstallerEngine engine)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException($"The {engine.Name} engine did not produce {what}. See the WiX output above.");
        }
    }
}
