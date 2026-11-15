using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace InstallKit.Tool
{
    /// <summary>The installkit commands. Output goes to the writers, so the commands can be tested.</summary>
    public static class Commands
    {
        public const string Usage = """
            installkit - tools for InstallKit installers

            Usage:
              installkit new-guid [count]          Print new GUIDs, for example for an UpgradeCode.
              installkit inspect <file.msi> [--json]   Show what an MSI installs (Windows only).
              installkit diff <old.msi> <new.msi>      Show what changed between two MSIs (Windows only).
              installkit --help                    Show this text.
            """;

        public static int Run(string[] args, TextWriter output, TextWriter error, Func<string, MsiSummary>? read = null)
        {
            read ??= MsiReader.Read;

            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                (args.Length == 0 ? error : output).WriteLine(Usage);
                return args.Length == 0 ? 2 : 0;
            }

            try
            {
                switch (args[0])
                {
                    case "new-guid":
                        return NewGuid(args.Skip(1).ToArray(), output, error);
                    case "inspect":
                        return Inspect(args.Skip(1).ToArray(), output, error, read);
                    case "diff":
                        return Diff(args.Skip(1).ToArray(), output, error, read);
                    default:
                        error.WriteLine($"Unknown command '{args[0]}'.");
                        error.WriteLine(Usage);
                        return 2;
                }
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is IOException || e is WixToolset.Dtf.WindowsInstaller.InstallerException)
            {
                error.WriteLine($"installkit: {e.Message}");
                return 1;
            }
        }

        static int NewGuid(string[] args, TextWriter output, TextWriter error)
        {
            var count = 1;
            if (args.Length > 1 || (args.Length == 1 && (!int.TryParse(args[0], out count) || count < 1 || count > 1000)))
            {
                error.WriteLine("Usage: installkit new-guid [count], with a count from 1 to 1000.");
                return 2;
            }

            for (var i = 0; i < count; i++)
                output.WriteLine(Guid.NewGuid().ToString("D"));
            return 0;
        }

        static int Inspect(string[] args, TextWriter output, TextWriter error, Func<string, MsiSummary> read)
        {
            var json = args.Contains("--json");
            var files = args.Where(a => a != "--json").ToArray();
            if (files.Length != 1)
            {
                error.WriteLine("Usage: installkit inspect <file.msi> [--json]");
                return 2;
            }

            var summary = Load(files[0], read);
            output.WriteLine(json ? ToJson(summary) : ToText(summary));
            return 0;
        }

        static int Diff(string[] args, TextWriter output, TextWriter error, Func<string, MsiSummary> read)
        {
            if (args.Length != 2)
            {
                error.WriteLine("Usage: installkit diff <old.msi> <new.msi>");
                return 2;
            }

            var entries = MsiDiff.Compare(Load(args[0], read), Load(args[1], read));
            if (entries.Count == 0)
            {
                output.WriteLine("No differences.");
                return 0;
            }

            foreach (var entry in entries)
                output.WriteLine(entry);
            return 0;
        }

        static MsiSummary Load(string path, Func<string, MsiSummary> read)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"'{path}' does not exist.");
            return read(path);
        }

        public static string ToJson(MsiSummary summary) =>
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });

        public static string ToText(MsiSummary summary)
        {
            var text = new StringBuilder();
            text.AppendLine($"{summary.ProductName} {summary.Version}");
            text.AppendLine($"  Manufacturer: {summary.Manufacturer}");
            text.AppendLine($"  UpgradeCode:  {summary.UpgradeCode}");
            text.AppendLine($"  ProductCode:  {summary.ProductCode}");
            text.AppendLine($"  Platform:     {summary.Platform}, {(summary.PerMachine ? "per machine" : "per user")}");

            Section(text, "Features", summary.Features);
            Section(text, "Files", summary.Files.Select(f => $"{f.Path} ({f.Size} bytes)"));
            Section(text, "Services", summary.Services);
            Section(text, "Registry values", summary.RegistryValues);
            Section(text, "Environment variables", summary.EnvironmentVariables);
            Section(text, "Shortcuts", summary.Shortcuts);
            Section(text, "Firewall rules", summary.FirewallRules);
            Section(text, "Custom actions", summary.CustomActions);
            return text.ToString().TrimEnd();
        }

        static void Section(StringBuilder text, string title, IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count == 0)
                return;

            text.AppendLine().AppendLine($"{title} ({list.Count})");
            foreach (var item in list)
                text.AppendLine("  " + item);
        }
    }
}
