using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class InstallEventTests
{
    static void StaticHandler(InstallEvent e) => e.Log("static");

    sealed class Stateful
    {
        public string Prefix = "x";

        public void Handle(InstallEvent e) => e.Log(Prefix);
    }

    [Fact]
    public void Static_methods_are_accepted()
    {
        Assert.Null(new InstallEventHandler(InstallStage.AfterInstall, StaticHandler).GetInstallTimeProblem());
    }

    [Fact]
    public void Lambdas_local_functions_and_instances_are_rejected()
    {
        var captured = "value";
        static void Local(InstallEvent e) => e.Log("local");

        Assert.NotNull(new InstallEventHandler(InstallStage.AfterInstall, e => e.Log("lambda")).GetInstallTimeProblem());
        Assert.NotNull(new InstallEventHandler(InstallStage.AfterInstall, e => e.Log(captured)).GetInstallTimeProblem());
        Assert.NotNull(new InstallEventHandler(InstallStage.AfterInstall, Local).GetInstallTimeProblem());
        Assert.NotNull(new InstallEventHandler(InstallStage.AfterInstall, new Stateful().Handle).GetInstallTimeProblem());
    }

    [Fact]
    public void Validator_reports_bad_handlers_colours_and_commands()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}" }));
        var captured = "value";

        plan.Msi(MsiBuilder.Create("App")
            .WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001")
            .WithPayload("App")
            .WithAccentColor("blue")
            .OnAfterInstall(e => e.Log(captured))
            .RunAfterInstall(""));

        var codes = InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();
        Assert.Contains("IK150", codes);
        Assert.Contains("IK151", codes);
        Assert.Contains("IK152", codes);
    }

    [Fact]
    public void Builder_collects_handlers_commands_and_branding()
    {
        var msi = MsiBuilder.Create("App")
            .WithModernUi(chooseInstallDir: true)
            .WithAccentColor("#112233")
            .OnBeforeInstall(StaticHandler)
            .OnAfterInstall(StaticHandler, "MODE", " ")
            .RunAfterInstall("tool.exe", "--init", c => c.Elevated = false)
            .RunBeforeUninstall("tool.exe", "--cleanup")
            .Build();

        Assert.Equal(InstallerUi.ModernInstallDir, msi.Ui);
        Assert.Equal("#112233", msi.Branding.AccentColor);
        Assert.Equal(new[] { InstallStage.BeforeInstall, InstallStage.AfterInstall }, msi.EventHandlers.Select(h => h.Stage));
        Assert.Equal(new[] { "MODE" }, msi.EventHandlers[1].UsesProperties);
        Assert.False(msi.Commands[0].Elevated);
        Assert.Equal(RunCommandTiming.BeforeUninstall, msi.Commands[1].Timing);
    }
}
