using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class CloseApplicationTests
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
    public void Builder_collects_rules_with_defaults()
    {
        var msi = MsiBuilder.Create("App")
            .WithCloseApplication("App.exe")
            .WithCloseApplication("Helper.exe", terminate: true, timeoutSeconds: 20)
            .Build();

        Assert.Equal(("App.exe", false, 5), (msi.CloseApplications[0].ProcessName, msi.CloseApplications[0].Terminate, msi.CloseApplications[0].TimeoutSeconds));
        Assert.Equal(("Helper.exe", true, 20), (msi.CloseApplications[1].ProcessName, msi.CloseApplications[1].Terminate, msi.CloseApplications[1].TimeoutSeconds));
        Assert.Single(new MsiDefinition("App").AddCloseApplication("App.exe").CloseApplications);
    }

    [Fact]
    public void Valid_rules_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithCloseApplication("App.exe").WithCloseApplication("Helper.exe", true, 600));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("App")]
    [InlineData(".exe")]
    [InlineData(@"bin\App.exe")]
    [InlineData("C:App.exe")]
    public void A_process_name_must_be_a_file_name(string name)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithCloseApplication(name));

        Assert.Contains("IK230", Codes(plan));
    }

    [Fact]
    public void Duplicates_are_reported_ignoring_case()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithCloseApplication("App.exe").WithCloseApplication("APP.EXE"));

        Assert.Contains("IK231", Codes(plan));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void The_timeout_must_be_reasonable(int seconds)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithCloseApplication("App.exe", timeoutSeconds: seconds));

        Assert.Contains("IK232", Codes(plan));
    }
}
