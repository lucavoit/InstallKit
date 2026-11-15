using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class RegistryTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").WithPayload("App");
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Builder_collects_registry_values_of_every_kind()
    {
        var msi = MsiBuilder.Create("App")
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "InstallPath", "[INSTALLDIR]")
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", null, "default value")
            .WithRegistryValue(RegistryHive.CurrentUser, @"Software\Contoso\App", "Logs", @"%LOCALAPPDATA%\App", RegistryValueKind.ExpandString)
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "Telemetry", 0)
            .Build();

        Assert.Equal(4, msi.RegistryEntries.Count);
        Assert.Equal("InstallPath", msi.RegistryEntries[0].Name);
        Assert.Null(msi.RegistryEntries[1].Name);
        Assert.Equal(RegistryValueKind.ExpandString, msi.RegistryEntries[2].Kind);
        Assert.Equal(RegistryValueKind.DWord, msi.RegistryEntries[3].Kind);
        Assert.Equal(0, msi.RegistryEntries[3].DWordValue);
    }

    [Fact]
    public void An_empty_name_is_the_default_value()
    {
        Assert.Null(new RegistryEntry(RegistryHive.LocalMachine, "Software\\A", "", "v").Name);
        Assert.Equal(@"HKLM\Software\A\(Default)", new RegistryEntry(RegistryHive.LocalMachine, @"Software\A", null, "v").ToString());
    }

    [Fact]
    public void Property_style_api_matches_the_builder()
    {
        var plan = NewPlan().Plan;
        var msi = plan.Msi("App", m => m.AddRegistryValue(RegistryHive.LocalMachine, @"Software\A", "N", "v").AddRegistryValue(RegistryHive.LocalMachine, @"Software\A", "D", 7));

        Assert.Equal(7, msi.RegistryEntries[1].DWordValue);
    }

    [Fact]
    public void Valid_registry_values_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "Mode", "x")
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "Level", 3));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"\Software\A")]
    public void A_bad_key_is_reported(string key)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithRegistryValue(RegistryHive.LocalMachine, key, "N", "v"));

        Assert.Contains("IK170", Codes(plan));
    }

    [Fact]
    public void A_dword_must_be_a_number()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithRegistryValue(RegistryHive.LocalMachine, @"Software\A", "N", "abc", RegistryValueKind.DWord));

        Assert.Contains("IK171", Codes(plan));
    }

    [Fact]
    public void Per_user_installs_cannot_write_hklm()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithScope(InstallScope.PerUser)
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\A", "N", "v")
            .WithRegistryValue(RegistryHive.CurrentUser, @"Software\A", "N", "v"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK172", error.Code);
        Assert.Contains("HKLM", error.Message);
    }
}
