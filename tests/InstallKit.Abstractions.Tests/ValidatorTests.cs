using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class ValidatorTests
{
    static (InstallerPlan Plan, string PayloadDir) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Svc.exe"), "");
        var context = InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir });
        return (new InstallerPlan(context), dir);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void A_complete_plan_is_valid()
    {
        var (plan, _) = NewPlan();
        var msi = plan.Msi("App", m =>
        {
            m.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001";
            m.AddPayload("App");
            m.MainExecutable = "App.exe";
            m.AddService("Svc.exe", "Svc");
        });
        plan.Bundle("App", b =>
        {
            b.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000002";
            b.Chain.NetFramework(NetFrameworkVersion.NetFx48).Msi(msi);
        });

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void Empty_plan_is_reported() => Assert.Contains("IK100", Codes(NewPlan().Plan));

    [Fact]
    public void Missing_upgrade_code_suggests_one()
    {
        var (plan, _) = NewPlan();
        plan.Msi("App", m => m.AddPayload("App"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK101", error.Code);
        Assert.Contains("UpgradeCode = \"", error.Message);
    }

    [Fact]
    public void Shared_upgrade_codes_are_rejected()
    {
        var (plan, _) = NewPlan();
        var msi = plan.Msi("App", m => { m.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001"; m.AddPayload("App"); });
        plan.Bundle("App", b => { b.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001"; b.Chain.Msi(msi); });

        Assert.Contains("IK103", Codes(plan));
    }

    [Fact]
    public void Unknown_payload_and_missing_executables_are_reported()
    {
        var (plan, _) = NewPlan();
        plan.Msi("App", m =>
        {
            m.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001";
            m.AddPayload("Nope");
            m.MainExecutable = "Missing.exe";
        });

        var codes = Codes(plan);
        Assert.Contains("IK111", codes);
        Assert.Contains("IK121", codes);
    }

    [Fact]
    public void Executables_resolve_through_target_subdirectories()
    {
        var (plan, _) = NewPlan();
        plan.Msi("App", m =>
        {
            m.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001";
            m.AddPayload("App", "bin");
            m.MainExecutable = @"bin\App.exe";
        });

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void Exe_prerequisites_need_a_detect_condition()
    {
        var (plan, dir) = NewPlan();
        plan.Bundle("App", b =>
        {
            b.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000002";
            b.Chain.Exe(Path.Combine(dir, "App.exe"));
        });

        Assert.Contains("IK134", Codes(plan));
    }

    [Fact]
    public void Chained_msi_must_belong_to_the_plan()
    {
        var (plan, _) = NewPlan();
        var other = new InstallerPlan(plan.Context).Msi("Other", m => { });
        plan.Bundle("App", b =>
        {
            b.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000002";
            b.Chain.Msi(other);
        });

        Assert.Contains("IK131", Codes(plan));
    }
}
