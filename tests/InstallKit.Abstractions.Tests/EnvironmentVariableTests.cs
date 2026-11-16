using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class EnvironmentVariableTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App"));
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Path_entries_append_by_default_and_can_prepend()
    {
        var msi = MsiBuilder.Create("App")
            .Machine(m => m
                .PathEntry("[INSTALLDIR]bin")
                .PathEntry("[INSTALLDIR]tools", prepend: true)
                .EnvironmentVariable("APP_HOME", "[INSTALLDIR]"))
            .Build();

        Assert.Equal(3, msi.EnvironmentVariables.Count);
        Assert.All(msi.EnvironmentVariables.Take(2), v => Assert.True(v.IsPath));
        Assert.Equal(EnvironmentVariableAction.Append, msi.EnvironmentVariables[0].Action);
        Assert.Equal(EnvironmentVariableAction.Prepend, msi.EnvironmentVariables[1].Action);
        Assert.Equal(EnvironmentVariableAction.Set, msi.EnvironmentVariables[2].Action);
        Assert.False(msi.EnvironmentVariables[2].IsPath);
    }

    [Fact]
    public void Property_style_api_matches_the_builder()
    {
        var msi = new MsiDefinition("App").AddPathEntry("[INSTALLDIR]bin").AddEnvironmentVariable("A", "b");

        Assert.Equal("PATH", msi.EnvironmentVariables[0].Name);
        Assert.Equal("b", msi.EnvironmentVariables[1].Value);
    }

    [Fact]
    public void Valid_variables_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.PathEntry("[INSTALLDIR]bin").EnvironmentVariable("path", "[INSTALLDIR]x", EnvironmentVariableAction.Prepend).EnvironmentVariable("APP_HOME", "[INSTALLDIR]")));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A=B")]
    public void A_bad_name_is_reported(string name)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.EnvironmentVariable(name, "x")));

        Assert.Contains("IK180", Codes(plan));
    }

    [Fact]
    public void An_empty_value_is_reported()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.EnvironmentVariable("A", " ")));

        Assert.Contains("IK181", Codes(plan));
    }

    [Fact]
    public void Setting_path_is_rejected_and_points_at_PathEntry()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.EnvironmentVariable("Path", "C:\\tools")));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK182", error.Code);
        Assert.Contains("PathEntry", error.Message);
    }

    [Fact]
    public void One_folder_per_path_entry()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.PathEntry("[INSTALLDIR]a;[INSTALLDIR]b")));

        Assert.Contains("IK183", Codes(plan));
    }
}
