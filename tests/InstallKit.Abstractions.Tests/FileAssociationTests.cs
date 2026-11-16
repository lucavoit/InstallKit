using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class FileAssociationTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Viewer.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("My App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App")).WithMainExecutable("App.exe");
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void A_file_association_becomes_progid_and_open_command_values()
    {
        var msi = MsiBuilder.Create("My App").WithMainExecutable("App.exe").Shell(s => s.FileAssociation(".foo", "Foo document")).Build();

        var entries = msi.ResolvedRegistryEntries;
        Assert.Equal(4, entries.Count);
        Assert.All(entries, e => Assert.Equal(RegistryHive.ClassesRoot, e.Hive));

        Assert.Equal((".foo", "MyApp.foo"), (entries[0].Key, entries[0].Value));
        Assert.Equal(("MyApp.foo", "Foo document"), (entries[1].Key, entries[1].Value));
        Assert.Equal(@"MyApp.foo\shell\open\command", entries[3].Key);
        Assert.Equal("\"[INSTALLDIR]App.exe\" \"%1\"", entries[3].Value);
        Assert.Empty(msi.RegistryEntries);
    }

    [Fact]
    public void The_executable_and_progid_can_be_chosen()
    {
        var msi = MsiBuilder.Create("My App").WithMainExecutable("App.exe")
            .Shell(s => s.FileAssociation(".bar", "Bar file", "tools/Viewer.exe", "Contoso.Bar"))
            .Build();

        var entries = msi.ResolvedRegistryEntries;
        Assert.Equal("Contoso.Bar", entries[0].Value);
        Assert.Equal("\"[INSTALLDIR]tools\\Viewer.exe\" \"%1\"", entries[3].Value);
    }

    [Fact]
    public void A_url_protocol_becomes_the_scheme_key_with_a_url_protocol_marker()
    {
        var msi = MsiBuilder.Create("My App").WithMainExecutable("App.exe").Shell(s => s.UrlProtocol("myapp", "My App link")).Build();

        var entries = msi.ResolvedRegistryEntries;
        Assert.Equal(("myapp", null, "URL:My App link"), (entries[0].Key, entries[0].Name, entries[0].Value));
        Assert.Equal(("myapp", "URL Protocol", ""), (entries[1].Key, entries[1].Name, entries[1].Value));
        Assert.Equal(@"myapp\shell\open\command", entries[3].Key);
    }

    [Fact]
    public void Resolved_entries_keep_the_plain_registry_values_first()
    {
        var msi = MsiBuilder.Create("My App").WithMainExecutable("App.exe")
            .Shell(s => s.FileAssociation(".foo", "Foo"))
            .Machine(m => m.Registry(RegistryHive.LocalMachine, @"Software\A", "N", "v"))
            .Build();

        Assert.Single(msi.RegistryEntries);
        Assert.Equal(@"Software\A", msi.ResolvedRegistryEntries[0].Key);
        Assert.Equal(5, msi.ResolvedRegistryEntries.Count);
    }

    [Fact]
    public void Valid_associations_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Shell(s => s.FileAssociation(".foo", "Foo").FileAssociation(".bar", "Bar", "Viewer.exe").UrlProtocol("my-app+1", "Link")));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("foo")]
    [InlineData(".")]
    [InlineData(".a b")]
    public void A_bad_extension_is_reported(string extension)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Shell(s => s.FileAssociation(extension, "x")));

        Assert.Contains("IK190", Codes(plan));
    }

    [Theory]
    [InlineData("myapp://")]
    [InlineData("1app")]
    [InlineData("")]
    public void A_bad_scheme_is_reported(string scheme)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Shell(s => s.UrlProtocol(scheme, "x")));

        Assert.Contains("IK191", Codes(plan));
    }

    [Fact]
    public void A_handler_needs_a_program_that_is_installed()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Shell(s => s.FileAssociation(".foo", "x", "Missing.exe")));
        Assert.Contains("IK121", Codes(plan));

        var withoutMain = new InstallerPlan(InstallerContext.FromEnvironment(new string[0]));
        withoutMain.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000003").Content(c => c.Directory(Path.GetTempPath())).Shell(s => s.UrlProtocol("myapp", "x")));
        Assert.Contains("IK192", Codes(withoutMain));
    }

    [Fact]
    public void Duplicates_and_bad_progids_are_reported()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Shell(s => s
            .FileAssociation(".foo", "x")
            .FileAssociation(".FOO", "y")
            .FileAssociation(".bar", "z", null, "1bad")
            .UrlProtocol("myapp", "a")
            .UrlProtocol("MyApp", "b")));

        var codes = Codes(plan);
        Assert.Contains("IK193", codes);
        Assert.Contains("IK194", codes);
        Assert.Contains("IK195", codes);
    }
}
