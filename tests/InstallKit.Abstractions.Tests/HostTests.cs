using System.Collections.Generic;
using System.IO;
using Xunit;

namespace InstallKit.Tests;

public class HostTests
{
    sealed class FakeEngine : IInstallerEngine
    {
        public List<string> Calls { get; } = new();

        public string Name => "Fake";

        public InstallerCapabilities Capabilities { get; set; } = InstallerCapabilities.None;

        public string BuildMsi(MsiDefinition msi, InstallerContext context)
        {
            Calls.Add("msi:" + msi.Name);
            var path = Path.Combine(context.OutputDirectory, msi.Name + ".msi");
            File.WriteAllText(path, "");
            return path;
        }

        public string BuildBundle(BundleDefinition bundle, IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context)
        {
            Calls.Add("bundle:" + bundle.Name + ":" + builtMsis.Count);
            var path = Path.Combine(context.OutputDirectory, bundle.Name + ".exe");
            File.WriteAllText(path, "");
            return path;
        }
    }

    sealed class RecordingSigner : IFileSigner
    {
        public List<string> Signed { get; } = new();

        public void Sign(string filePath) => Signed.Add(Path.GetFileName(filePath));
    }

    [Fact]
    public void Msis_are_built_and_signed_before_bundles()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--output", dir, "--intermediate", Path.Combine(dir, "obj") }));
        var signer = new RecordingSigner();
        plan.UseSigner(signer);

        var msi = plan.Msi("App", m => { m.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000001"; m.AddDirectory(dir); });
        plan.Bundle("Setup", b => { b.UpgradeCode = "2f0a4e2c-1111-4c1e-9b1a-000000000002"; b.Chain.Msi(msi); });

        var engine = new FakeEngine();
        var outputs = InstallerHost.Build(plan, engine);

        Assert.Equal(new[] { "msi:App", "bundle:Setup:1" }, engine.Calls);
        Assert.Equal(2, outputs.Count);
        Assert.Equal(new[] { "App.msi" }, signer.Signed); // bundle signing is the engine's job
    }

    [Fact]
    public void Modern_ui_falls_back_when_the_engine_lacks_it()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--output", dir, "--intermediate", Path.Combine(dir, "obj") }));
        var msi = plan.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Directory(dir)).Appearance(a => a.Modern(true)));

        InstallerHost.Build(plan, new FakeEngine());

        Assert.Equal(InstallerUi.InstallDir, msi.Ui);
    }

    static MsiDefinition BuildWith(InstallerCapabilities capabilities, System.Action<MsiBuilder>? configure = null)
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--output", dir, "--intermediate", Path.Combine(dir, "obj") }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Directory(dir));
        configure?.Invoke(builder);
        var msi = plan.Msi(builder);

        InstallerHost.Build(plan, new FakeEngine { Capabilities = capabilities });
        return msi;
    }

    [Fact]
    public void The_default_ui_is_the_flat_dialogs_when_the_engine_has_wpf()
    {
        Assert.Equal(InstallerUi.Default, new MsiDefinition("App").Ui);
        Assert.Equal(InstallerUi.ModernFlat, BuildWith(InstallerCapabilities.ModernUi | InstallerCapabilities.WpfUi).Ui);
    }

    [Fact]
    public void The_default_ui_is_the_native_modern_dialogs_without_wpf()
    {
        Assert.Equal(InstallerUi.ModernInstallDir, BuildWith(InstallerCapabilities.ModernUi).Ui);
        Assert.Equal(InstallerUi.Minimal, BuildWith(InstallerCapabilities.None).Ui);
    }

    [Fact]
    public void A_ui_that_was_chosen_is_kept_and_only_falls_back_with_a_warning()
    {
        Assert.Equal(InstallerUi.Minimal, BuildWith(InstallerCapabilities.ModernUi | InstallerCapabilities.WpfUi, m => m.Appearance(a => a.Classic(InstallerUi.Minimal))).Ui);
        Assert.Equal(InstallerUi.ModernInstallDir, BuildWith(InstallerCapabilities.ModernUi, m => m.Appearance(a => a.Flat())).Ui);
    }

    [Fact]
    public void Install_events_need_engine_support()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--output", dir }));
        plan.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Directory(dir)).Lifecycle(l => l.OnLoad(e => e.Log("hi"))));

        var ex = Assert.Throws<InstallerValidationException>(() => InstallerHost.Build(plan, new FakeEngine()));
        Assert.Equal("IK201", ex.Errors[0].Code);
    }

    [Fact]
    public void Validation_errors_stop_the_build()
    {
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new string[0]));
        plan.Msi("App", _ => { });

        Assert.Throws<InstallerValidationException>(() => InstallerHost.Build(plan, new FakeEngine()));
    }
}
