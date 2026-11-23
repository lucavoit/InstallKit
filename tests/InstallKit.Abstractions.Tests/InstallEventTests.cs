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
            .Content(c => c.Payload("App"))
            .Appearance(a => a.AccentColor("blue"))
            .Lifecycle(l => l
                .OnAfterInstall(e => e.Log(captured))
                .RunAfterInstall("")));

        var codes = InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();
        Assert.Contains("IK150", codes);
        Assert.Contains("IK151", codes);
        Assert.Contains("IK152", codes);
    }

    [Fact]
    public void Builder_collects_handlers_commands_and_branding()
    {
        var msi = MsiBuilder.Create("App")
            .Appearance(a => a
                .Modern(chooseInstallDir: true)
                .AccentColor("#112233"))
            .Lifecycle(l => l
                .OnBeforeInstall(StaticHandler)
                .OnAfterInstall(StaticHandler, "MODE", " ")
                .RunAfterInstall("tool.exe", "--init", c => c.Elevated = false)
                .RunBeforeUninstall("tool.exe", "--cleanup"))
            .Build();

        Assert.Equal(InstallerUi.ModernInstallDir, msi.Ui);
        Assert.Equal("#112233", msi.Branding.AccentColor);
        Assert.Equal(new[] { InstallStage.BeforeInstall, InstallStage.AfterInstall }, msi.EventHandlers.Select(h => h.Stage));
        Assert.Equal(new[] { "MODE" }, msi.EventHandlers[1].UsesProperties);
        Assert.False(msi.Commands[0].Elevated);
        Assert.Equal(RunCommandTiming.BeforeUninstall, msi.Commands[1].Timing);
    }

    sealed class FakeEvent : InstallEvent
    {
        public bool Installing, Upgrading;
        public string? Sid;
        public override InstallStage Stage => InstallStage.AfterInstall;
        public override bool IsInstalling => Installing;
        public override bool IsUninstalling => false;
        public override bool IsUpgrading => Upgrading;
        public override bool IsRepairing => false;
        public override bool IsUiSuppressed => false;
        public override string? InstallDir => null;
        public override string? this[string property] => null;
        public override void Log(string message) { }
        public override string? InstallingUserSid => Sid;
    }

    [Fact]
    public void A_fresh_install_is_an_install_that_is_not_an_upgrade()
    {
        Assert.True(new FakeEvent { Installing = true }.IsFreshInstall);
        Assert.False(new FakeEvent { Installing = true, Upgrading = true }.IsFreshInstall);
        Assert.False(new FakeEvent().IsFreshInstall);
    }

    [Fact]
    public void Only_the_system_account_counts_as_installed_by_system()
    {
        Assert.True(new FakeEvent { Sid = "S-1-5-18" }.IsInstalledBySystem);
        Assert.False(new FakeEvent { Sid = "S-1-5-21-1-2-3-1001" }.IsInstalledBySystem);
        Assert.False(new FakeEvent().IsInstalledBySystem);
    }

    [Fact]
    public void Fail_cancels_with_a_message_in_every_stage()
    {
        var e = new FakeEvent();
        e.Fail("No licence key.");

        Assert.True(e.IsCanceled);
        Assert.Equal("No licence key.", e.CancelReason);
    }

    [Fact]
    public void A_failure_mode_can_be_chosen_for_after_install_handlers()
    {
        var msi = MsiBuilder.Create("App")
            .Lifecycle(l => l
                .OnAfterInstall(StaticHandler, InstallFailureMode.Warn, "MODE")
                .OnAfterInstall(StaticHandler))
            .Build();

        Assert.Equal(new[] { InstallFailureMode.Warn, InstallFailureMode.Rollback }, msi.EventHandlers.Select(h => h.FailureMode));
        Assert.Equal(new[] { "MODE" }, msi.EventHandlers[0].UsesProperties);
        Assert.Equal("Sample_files", OptionalFeature.IdOf("Sample files"));
    }
}
