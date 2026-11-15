using System;
using System.Collections.Generic;
using System.Linq;
using WixToolset.Dtf.WindowsInstaller;

namespace InstallKit.Tool
{
    /// <summary>Reads an MSI file with the Windows Installer API, so it only works on Windows.</summary>
    public static class MsiReader
    {
        public static MsiSummary Read(string path)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Reading an MSI needs Windows, because it uses the Windows Installer API.");

            using (var db = new Database(path, DatabaseOpenMode.ReadOnly))
            {
                var properties = Rows(db, "Property", "Property", "Value").ToDictionary(r => r[0]!, r => r[1], StringComparer.Ordinal);
                string? Property(string name) => properties.TryGetValue(name, out var value) ? value : null;

                var summary = new MsiSummary
                {
                    ProductName = Property("ProductName"),
                    Version = Property("ProductVersion"),
                    Manufacturer = Property("Manufacturer"),
                    UpgradeCode = Property("UpgradeCode"),
                    ProductCode = Property("ProductCode"),
                    PerMachine = Property("ALLUSERS") == "1",
                    Platform = db.SummaryInfo.Template?.Split(';').FirstOrDefault(),
                };

                summary.Features = Rows(db, "Feature", "Feature", "Title", "Level")
                    .Select(r => $"{r[0]} ({r[1] ?? r[0]}, level {r[2]})").OrderBy(x => x, StringComparer.Ordinal).ToList();

                var directories = ReadDirectories(db);
                var componentDirectories = Rows(db, "Component", "Component", "Directory_").ToDictionary(r => r[0]!, r => r[1]!, StringComparer.Ordinal);
                summary.Files = Rows(db, "File", "Component_", "FileName", "FileSize")
                    .Select(r => new MsiFile
                    {
                        Path = Combine(directories.TryGetValue(componentDirectories.TryGetValue(r[0]!, out var dir) ? dir : string.Empty, out var folder) ? folder : "?", LongName(r[1])),
                        Size = long.TryParse(r[2], out var size) ? size : 0,
                    })
                    .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();

                summary.Services = Rows(db, "ServiceInstall", "Name", "DisplayName", "StartType")
                    .Select(r => $"{r[0]} ({r[1] ?? r[0]}, start type {r[2]})").OrderBy(x => x, StringComparer.Ordinal).ToList();

                summary.RegistryValues = Rows(db, "Registry", "Root", "Key", "Name", "Value")
                    .Select(r => $"{RootName(r[0])}\\{r[1]}\\{(string.IsNullOrEmpty(r[2]) ? "(Default)" : r[2])} = {r[3]}").OrderBy(x => x, StringComparer.Ordinal).ToList();

                summary.EnvironmentVariables = Rows(db, "Environment", "Name", "Value")
                    .Select(r => $"{r[0]} = {r[1]}").OrderBy(x => x, StringComparer.Ordinal).ToList();

                summary.Shortcuts = Rows(db, "Shortcut", "Name", "Directory_", "Target")
                    .Select(r => $"{LongName(r[0])} in {r[1]} -> {r[2]}").OrderBy(x => x, StringComparer.Ordinal).ToList();

                summary.CustomActions = Rows(db, "CustomAction", "Action", "Type")
                    .Select(r => $"{r[0]} (type {r[1]})").OrderBy(x => x, StringComparer.Ordinal).ToList();

                // WiX 4 and later prefix the extension's table names with "Wix4"; older MSIs have "Wix".
                summary.FirewallRules = Rows(db, db.Tables.Contains("Wix4FirewallException") ? "Wix4FirewallException" : "WixFirewallException", "Name", "Port", "Program")
                    .Select(r => $"{r[0]} (port {r[1] ?? "any"}, program {r[2] ?? "any"})").OrderBy(x => x, StringComparer.Ordinal).ToList();

                return summary;
            }
        }

        // Rows of a table (or none when the MSI has no such table), with every column as text or null.
        static List<string?[]> Rows(Database db, string table, params string[] columns)
        {
            var rows = new List<string?[]>();
            if (!db.Tables.Contains(table))
                return rows;

            var select = string.Join(", ", columns.Select(c => $"`{c}`"));
            using (var view = db.OpenView($"SELECT {select} FROM `{table}`"))
            {
                view.Execute();
                foreach (var record in view)
                {
                    using (record)
                    {
                        var row = new string?[columns.Length];
                        for (var i = 0; i < columns.Length; i++)
                            row[i] = record.IsNull(i + 1) ? null : record.GetString(i + 1);
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        // Directory id -> "[Id]" or "[Parent]child\grandchild", so paths read like the folders an install creates.
        static Dictionary<string, string> ReadDirectories(Database db)
        {
            var rows = Rows(db, "Directory", "Directory", "Directory_Parent", "DefaultDir").ToDictionary(r => r[0]!, r => (Parent: r[1], Name: LongName(r[2])), StringComparer.Ordinal);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            string Resolve(string id, int depth)
            {
                if (depth > 32 || !rows.TryGetValue(id, out var row) || string.IsNullOrEmpty(row.Parent) || row.Parent == id)
                    return $"[{id}]";
                // A name of "." means the same folder as the parent.
                return row.Name == "." ? Resolve(row.Parent!, depth + 1) : Combine(Resolve(row.Parent!, depth + 1), row.Name);
            }

            foreach (var id in rows.Keys)
                result[id] = Resolve(id, 0);
            return result;
        }

        static string Combine(string folder, string name) => folder.EndsWith("\\", StringComparison.Ordinal) ? folder + name : folder + "\\" + name;

        // Short and long names are stored as "SHORT~1|Long name".
        static string LongName(string? name)
        {
            if (name == null)
                return string.Empty;
            var bar = name.IndexOf('|');
            return bar >= 0 ? name.Substring(bar + 1) : name;
        }

        static string RootName(string? root) => root switch
        {
            "0" => "HKCR",
            "1" => "HKCU",
            "2" => "HKLM",
            "3" => "HKU",
            "-1" => "HKMU",
            _ => root ?? "?",
        };
    }
}
