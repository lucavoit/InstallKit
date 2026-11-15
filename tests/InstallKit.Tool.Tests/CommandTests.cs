using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using InstallKit.Tool;
using Xunit;

namespace InstallKit.Tool.Tests;

public class CommandTests
{
    static MsiSummary Product(string version = "1.0.0.0") => new()
    {
        ProductName = "My App",
        Version = version,
        Manufacturer = "Contoso",
        UpgradeCode = "0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10",
        ProductCode = "11111111-1111-1111-1111-111111111111",
        Platform = "x64",
        PerMachine = true,
        Features = { "Complete (Complete, level 1)" },
        Files = { new MsiFile { Path = @"[INSTALLDIR]\MyApp.exe", Size = 100 }, new MsiFile { Path = @"[INSTALLDIR]\readme.txt", Size = 5 } },
        Services = { "MyService (My Service, start type 2)" },
        RegistryValues = { @"HKLM\Software\Contoso\My App\Mode = x" },
    };

    static (int Code, string Output, string Error) Run(Func<string, MsiSummary>? read, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Commands.Run(args, output, error, read);
        return (code, output.ToString(), error.ToString());
    }

    static string TempFile()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "x.msi");
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Help_is_printed_and_no_arguments_is_a_usage_error()
    {
        Assert.Equal(0, Run(null, "--help").Code);
        Assert.Contains("new-guid", Run(null, "--help").Output);

        var none = Run(null);
        Assert.Equal(2, none.Code);
        Assert.Contains("Usage", none.Error);
    }

    [Fact]
    public void An_unknown_command_is_a_usage_error()
    {
        var result = Run(null, "frobnicate");

        Assert.Equal(2, result.Code);
        Assert.Contains("frobnicate", result.Error);
    }

    [Fact]
    public void New_guid_prints_the_requested_number_of_distinct_guids()
    {
        var lines = Run(null, "new-guid", "3").Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Equal(3, lines.Distinct().Count());
        Assert.All(lines, l => Assert.True(Guid.TryParse(l, out var g) && g != Guid.Empty && l == g.ToString("D")));
        Assert.Single(Run(null, "new-guid").Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1001")]
    [InlineData("many")]
    public void New_guid_rejects_a_bad_count(string count) => Assert.Equal(2, Run(null, "new-guid", count).Code);

    [Fact]
    public void Inspect_prints_the_product_and_its_parts()
    {
        var result = Run(_ => Product(), "inspect", TempFile());

        Assert.Equal(0, result.Code);
        Assert.Contains("My App 1.0.0.0", result.Output);
        Assert.Contains("UpgradeCode:  0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10", result.Output);
        Assert.Contains("Files (2)", result.Output);
        Assert.Contains(@"[INSTALLDIR]\MyApp.exe (100 bytes)", result.Output);
        Assert.Contains("Services (1)", result.Output);
        Assert.DoesNotContain("Shortcuts", result.Output);
    }

    [Fact]
    public void Inspect_json_round_trips()
    {
        var result = Run(_ => Product(), "inspect", TempFile(), "--json");

        var summary = JsonSerializer.Deserialize<MsiSummary>(result.Output)!;
        Assert.Equal("Contoso", summary.Manufacturer);
        Assert.Equal(2, summary.Files.Count);
        Assert.True(summary.PerMachine);
    }

    [Fact]
    public void Inspect_needs_one_existing_file()
    {
        Assert.Equal(2, Run(_ => Product(), "inspect").Code);
        Assert.Equal(1, Run(_ => Product(), "inspect", Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".msi")).Code);
    }

    [Fact]
    public void A_platform_that_cannot_read_msis_is_reported_not_thrown()
    {
        var result = Run(_ => throw new PlatformNotSupportedException("Reading an MSI needs Windows."), "inspect", TempFile());

        Assert.Equal(1, result.Code);
        Assert.Contains("needs Windows", result.Error);
    }

    [Fact]
    public void Diff_of_equal_products_says_so()
    {
        var result = Run(_ => Product(), "diff", TempFile(), TempFile());

        Assert.Equal(0, result.Code);
        Assert.Equal("No differences.", result.Output.Trim());
    }

    [Fact]
    public void Diff_lists_added_removed_and_changed_parts()
    {
        var before = Product();
        var after = Product("1.1.0.0");
        after.Files.RemoveAt(1);                                                       // readme removed
        after.Files[0].Size = 150;                                                     // exe changed
        after.Files.Add(new MsiFile { Path = @"[INSTALLDIR]\docs\guide.pdf", Size = 9 }); // guide added
        after.Services.Clear();
        after.RegistryValues.Add(@"HKLM\Software\Contoso\My App\Telemetry = 0");
        after.Features.Add("Docs (Documentation, level 1)");

        var entries = MsiDiff.Compare(before, after).Select(e => e.ToString()).ToList();

        Assert.Contains("~ version: 1.0.0.0 -> 1.1.0.0", entries);
        Assert.Contains(@"+ file: [INSTALLDIR]\docs\guide.pdf (9 bytes)", entries);
        Assert.Contains(@"- file: [INSTALLDIR]\readme.txt", entries);
        Assert.Contains(@"~ file: [INSTALLDIR]\MyApp.exe (100 -> 150 bytes)", entries);
        Assert.Contains("- service: MyService (My Service, start type 2)", entries);
        Assert.Contains(@"+ registry value: HKLM\Software\Contoso\My App\Telemetry = 0", entries);
        Assert.Contains("+ feature: Docs (Documentation, level 1)", entries);
        Assert.DoesNotContain(entries, e => e.Contains("upgrade code"));
    }

    [Fact]
    public void Diff_is_case_insensitive_for_file_paths()
    {
        var before = Product();
        var after = Product();
        after.Files[0].Path = after.Files[0].Path.ToUpperInvariant();

        Assert.DoesNotContain(MsiDiff.Compare(before, after), e => e.Area == "file" && e.Kind != DiffKind.Changed);
    }

    [Fact]
    public void Diff_needs_two_files()
    {
        Assert.Equal(2, Run(_ => Product(), "diff", TempFile()).Code);
    }
}
