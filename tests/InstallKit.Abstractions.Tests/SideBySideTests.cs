using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class SideBySideTests
{
    const string BaseCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001";
    const string BundleCode = "2f0a4e2c-1111-4c1e-9b1a-000000000002";

    static InstallerPlan NewPlan(string version = "3.1.0")
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Svc.exe"), "");
        return new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--version", version, "--payload", $"App={dir}", "--output", dir }));
    }

    static MsiBuilder App(SideBySideMode mode) =>
        MsiBuilder.Create("My App").WithManufacturer("Contoso").WithUpgradeCode(BaseCode).Content(c => c.Payload("App")).WithSideBySide(mode)
            .Machine(m => m.Service("Svc.exe", "MySvc", s => s.WithDisplayName("My Service")));

    [Theory]
    [InlineData("3.5.6.9", SideBySideMode.None, "")]
    [InlineData("3.5.6.9", SideBySideMode.Major, "3")]
    [InlineData("3.5.6.9", SideBySideMode.MajorMinor, "3.5")]
    [InlineData("3.5.6.9", SideBySideMode.Full, "3.5.6")]
    [InlineData("3.5", SideBySideMode.Full, "3.5.0")]
    public void The_line_follows_the_mode(string version, SideBySideMode mode, string line) =>
        Assert.Equal(line, SideBySide.LineOf(Version.Parse(version), mode));

    [Fact]
    public void The_derived_upgrade_code_is_stable_distinct_per_line_and_a_valid_guid()
    {
        var code = Guid.Parse(BaseCode);

        Assert.Equal(SideBySide.DeriveUpgradeCode(code, "3.1"), SideBySide.DeriveUpgradeCode(code, "3.1"));
        Assert.NotEqual(SideBySide.DeriveUpgradeCode(code, "3.1"), SideBySide.DeriveUpgradeCode(code, "3.5"));
        Assert.NotEqual(code, SideBySide.DeriveUpgradeCode(code, "3.1"));
        Assert.NotEqual(SideBySide.DeriveUpgradeCode(code, "3.1"), SideBySide.DeriveUpgradeCode(Guid.Parse(BundleCode), "3.1"));
        Assert.NotEqual(Guid.Empty, SideBySide.DeriveUpgradeCode(code, "1"));
    }

    [Fact]
    public void Without_a_mode_nothing_changes()
    {
        var plan = NewPlan();
        var msi = plan.Msi(App(SideBySideMode.None));

        SideBySide.Apply(plan);

        Assert.Equal("My App", msi.Name);
        Assert.Equal(BaseCode, msi.UpgradeCode);
        Assert.Equal("MySvc", msi.Services[0].Name);
    }

    [Fact]
    public void A_mode_gives_the_name_upgrade_code_and_services_the_line()
    {
        var plan = NewPlan("3.1.0");
        var msi = plan.Msi(App(SideBySideMode.MajorMinor));

        SideBySide.Apply(plan);

        Assert.Equal("My App 3.1", msi.Name);
        Assert.Equal(SideBySide.DeriveUpgradeCode(Guid.Parse(BaseCode), "3.1").ToString("D"), msi.UpgradeCode);
        Assert.Equal("MySvc-3.1", msi.Services[0].Name);
        Assert.Equal("My Service 3.1", msi.Services[0].DisplayName);
        Assert.Equal(@"%ProgramFiles%\Contoso\My App 3.1", msi.ResolvedInstallDirectory);
        Assert.Equal("MyApp3.1", msi.ResolvedOutputFileName);
    }

    [Fact]
    public void Applying_twice_changes_nothing_more()
    {
        var plan = NewPlan();
        var msi = plan.Msi(App(SideBySideMode.Major));

        SideBySide.Apply(plan);
        var once = (msi.Name, msi.UpgradeCode, msi.Services[0].Name);
        SideBySide.Apply(plan);

        Assert.Equal(once, (msi.Name, msi.UpgradeCode, msi.Services[0].Name));
    }

    [Fact]
    public void Versions_of_one_line_replace_each_other_and_other_lines_do_not()
    {
        string CodeFor(string version, SideBySideMode mode)
        {
            var plan = NewPlan(version);
            var msi = plan.Msi(App(mode));
            SideBySide.Apply(plan);
            return msi.UpgradeCode!;
        }

        Assert.Equal(CodeFor("3.1.0", SideBySideMode.MajorMinor), CodeFor("3.1.9", SideBySideMode.MajorMinor));
        Assert.NotEqual(CodeFor("3.1.0", SideBySideMode.MajorMinor), CodeFor("3.5.6", SideBySideMode.MajorMinor));
        Assert.Equal(CodeFor("3.1.0", SideBySideMode.Major), CodeFor("3.5.6", SideBySideMode.Major));
        Assert.NotEqual(CodeFor("3.1.0", SideBySideMode.Major), CodeFor("4.0.0", SideBySideMode.Major));
        Assert.NotEqual(CodeFor("3.1.0", SideBySideMode.Full), CodeFor("3.1.1", SideBySideMode.Full));
    }

    [Fact]
    public void The_version_of_the_msi_wins_over_the_build_version()
    {
        var plan = NewPlan("9.9.9");
        var msi = plan.Msi(App(SideBySideMode.MajorMinor).WithVersion(new Version(3, 5, 6)));

        SideBySide.Apply(plan);

        Assert.Equal("My App 3.5", msi.Name);
    }

    [Fact]
    public void A_missing_or_invalid_upgrade_code_is_left_for_the_validator()
    {
        var plan = NewPlan();
        var missing = plan.Msi(MsiBuilder.Create("A").Content(c => c.Payload("App")).WithSideBySide(SideBySideMode.Major));
        var invalid = plan.Msi(MsiBuilder.Create("B").WithUpgradeCode("nope").Content(c => c.Payload("App")).WithSideBySide(SideBySideMode.Major));

        SideBySide.Apply(plan);

        Assert.Null(missing.UpgradeCode);
        Assert.Equal("nope", invalid.UpgradeCode);
        var codes = InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();
        Assert.Contains("IK101", codes);
        Assert.Contains("IK102", codes);
    }

    [Fact]
    public void An_install_directory_must_contain_the_line()
    {
        var plan = NewPlan("3.1.0");
        plan.Msi(App(SideBySideMode.MajorMinor).WithInstallDirectory(@"C:\Apps\My App"));
        SideBySide.Apply(plan);

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK270", error.Code);
        Assert.Contains("3.1", error.Message);

        var good = NewPlan("3.1.0");
        good.Msi(App(SideBySideMode.MajorMinor).WithInstallDirectory(@"C:\Apps\My App 3.1"));
        SideBySide.Apply(good);
        Assert.Empty(InstallerValidator.Validate(good));
    }

    [Fact]
    public void A_bundle_that_chains_side_by_side_msis_must_be_side_by_side_too()
    {
        var plan = NewPlan();
        var msi = plan.Msi(App(SideBySideMode.MajorMinor));
        plan.Bundle(BundleBuilder.Create("My App").WithUpgradeCode(BundleCode).Packages(k => k.Msi(msi)));
        SideBySide.Apply(plan);

        Assert.Contains("IK271", InstallerValidator.Validate(plan).Select(e => e.Code));
    }

    [Fact]
    public void A_side_by_side_bundle_gets_its_own_line_too()
    {
        var plan = NewPlan("3.1.0");
        var msi = plan.Msi(App(SideBySideMode.MajorMinor));
        var bundle = plan.Bundle(BundleBuilder.Create("My App").WithUpgradeCode(BundleCode).WithSideBySide(SideBySideMode.MajorMinor).Packages(k => k.Msi(msi)));

        SideBySide.Apply(plan);

        Assert.Equal("My App 3.1", bundle.Name);
        Assert.Equal("MyApp3.1Setup", bundle.ResolvedOutputFileName);
        Assert.Equal(SideBySide.DeriveUpgradeCode(Guid.Parse(BundleCode), "3.1").ToString("D"), bundle.UpgradeCode);
        Assert.NotEqual(msi.UpgradeCode, bundle.UpgradeCode);
        Assert.Empty(InstallerValidator.Validate(plan));
    }

    [Fact]
    public void The_host_applies_it_before_validation_and_building()
    {
        var plan = NewPlan("3.5.6");
        plan.Msi(App(SideBySideMode.MajorMinor));
        var engine = new RecordingEngine();

        InstallerHost.Build(plan, engine);

        Assert.Equal(new[] { "My App 3.5" }, engine.Built);
    }

    sealed class RecordingEngine : IInstallerEngine
    {
        public System.Collections.Generic.List<string> Built { get; } = new();

        public string Name => "Recording";

        public InstallerCapabilities Capabilities => InstallerCapabilities.None;

        public string BuildMsi(MsiDefinition msi, InstallerContext context)
        {
            Built.Add(msi.Name);
            var path = Path.Combine(context.OutputDirectory, msi.ResolvedOutputFileName + ".msi");
            File.WriteAllText(path, "");
            return path;
        }

        public string BuildBundle(BundleDefinition bundle, System.Collections.Generic.IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context) =>
            throw new NotSupportedException();
    }
}
