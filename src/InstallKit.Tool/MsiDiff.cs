using System;
using System.Collections.Generic;
using System.Linq;

namespace InstallKit.Tool
{
    public enum DiffKind
    {
        Added,
        Removed,
        Changed,
    }

    public sealed class DiffEntry
    {
        public DiffEntry(DiffKind kind, string area, string description)
        {
            Kind = kind;
            Area = area;
            Description = description;
        }

        public DiffKind Kind { get; }

        /// <summary>For example <c>file</c> or <c>service</c>.</summary>
        public string Area { get; }

        public string Description { get; }

        public override string ToString() => $"{(Kind == DiffKind.Added ? "+" : Kind == DiffKind.Removed ? "-" : "~")} {Area}: {Description}";
    }

    /// <summary>Compares two MSIs by what they install, so a pull request can say what a change does to the product.</summary>
    public static class MsiDiff
    {
        public static IReadOnlyList<DiffEntry> Compare(MsiSummary before, MsiSummary after)
        {
            var entries = new List<DiffEntry>();

            Scalar(entries, "product name", before.ProductName, after.ProductName);
            Scalar(entries, "version", before.Version, after.Version);
            Scalar(entries, "manufacturer", before.Manufacturer, after.Manufacturer);
            Scalar(entries, "upgrade code", before.UpgradeCode, after.UpgradeCode);
            Scalar(entries, "platform", before.Platform, after.Platform);
            Scalar(entries, "scope", before.PerMachine ? "per machine" : "per user", after.PerMachine ? "per machine" : "per user");

            Sets(entries, "feature", before.Features, after.Features);
            Sets(entries, "service", before.Services, after.Services);
            Sets(entries, "registry value", before.RegistryValues, after.RegistryValues);
            Sets(entries, "environment variable", before.EnvironmentVariables, after.EnvironmentVariables);
            Sets(entries, "shortcut", before.Shortcuts, after.Shortcuts);
            Sets(entries, "custom action", before.CustomActions, after.CustomActions);
            Sets(entries, "firewall rule", before.FirewallRules, after.FirewallRules);

            var oldFiles = before.Files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var newFiles = after.Files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var file in newFiles.Values.Where(f => !oldFiles.ContainsKey(f.Path)).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
                entries.Add(new DiffEntry(DiffKind.Added, "file", $"{file.Path} ({file.Size} bytes)"));
            foreach (var file in oldFiles.Values.Where(f => !newFiles.ContainsKey(f.Path)).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
                entries.Add(new DiffEntry(DiffKind.Removed, "file", file.Path));
            foreach (var file in newFiles.Values.Where(f => oldFiles.TryGetValue(f.Path, out var old) && old.Size != f.Size).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
                entries.Add(new DiffEntry(DiffKind.Changed, "file", $"{file.Path} ({oldFiles[file.Path].Size} -> {file.Size} bytes)"));

            return entries;
        }

        static void Scalar(List<DiffEntry> entries, string area, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                entries.Add(new DiffEntry(DiffKind.Changed, area, $"{before ?? "(none)"} -> {after ?? "(none)"}"));
        }

        static void Sets(List<DiffEntry> entries, string area, IEnumerable<string> before, IEnumerable<string> after)
        {
            var oldSet = new HashSet<string>(before, StringComparer.Ordinal);
            var newSet = new HashSet<string>(after, StringComparer.Ordinal);
            foreach (var item in newSet.Where(i => !oldSet.Contains(i)).OrderBy(i => i, StringComparer.Ordinal))
                entries.Add(new DiffEntry(DiffKind.Added, area, item));
            foreach (var item in oldSet.Where(i => !newSet.Contains(i)).OrderBy(i => i, StringComparer.Ordinal))
                entries.Add(new DiffEntry(DiffKind.Removed, area, item));
        }
    }
}
