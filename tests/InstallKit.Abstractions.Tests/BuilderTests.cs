using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class BuilderTests
{
    [Fact]
    public void Create_keeps_model_defaults()
    {
        var msi = MsiBuilder.Create("App").Build();
        var expected = new MsiDefinition("App");

        Assert.Equal("App", msi.Name);
        Assert.Equal(expected.Ui, msi.Ui);
        Assert.Equal(expected.Platform, msi.Platform);
        Assert.Equal(expected.Scope, msi.Scope);
        Assert.True(msi.Shortcuts.StartMenu);
        Assert.Empty(msi.Payloads);

        var service = ServiceBuilder.Create("Svc.exe", "Svc").Build();
        Assert.True(service.StartOnInstall);
        Assert.Equal(ServiceStartMode.Automatic, service.StartMode);
    }

    [Fact]
    public void Fluent_msi_matches_property_style()
    {
        var msi = MsiBuilder.Create("App")
            .WithManufacturer("Contoso")
            .WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001")
            .WithVersion(new Version(2, 0, 1))
            .WithUi(InstallerUi.InstallDir)
            .WithPayload("App")
            .WithPayload("Tools", "tools")
            .WithMainExecutable("App.exe")
            .WithDesktopShortcut()
            .WithStartMenuShortcut(false)
            .WithService("Svc.exe", "Svc", s => s.WithDisplayName("My service").WithStartMode(ServiceStartMode.Manual))
            .WithProperty("MODE", "server")
            .Build();

        Assert.Equal("Contoso", msi.Manufacturer);
        Assert.Equal(new Version(2, 0, 1), msi.Version);
        Assert.Equal(InstallerUi.InstallDir, msi.Ui);
        Assert.Equal(new[] { "App", "Tools" }, msi.Payloads.Select(p => p.PayloadName));
        Assert.Equal("tools", msi.Payloads[1].TargetSubdirectory);
        Assert.True(msi.Shortcuts.Desktop);
        Assert.False(msi.Shortcuts.StartMenu);
        var service = Assert.Single(msi.Services);
        Assert.Equal(("Svc.exe", "Svc", "My service", ServiceStartMode.Manual), (service.Executable, service.Name, service.DisplayName, service.StartMode));
        Assert.Equal("server", msi.Properties["MODE"]);
    }

    [Fact]
    public void Builders_do_not_share_collections()
    {
        var first = MsiBuilder.Create("A").WithPayload("A").Build();
        var second = MsiBuilder.Create("B").Build();

        Assert.Single(first.Payloads);
        Assert.Empty(second.Payloads);
    }

    [Fact]
    public void Fluent_plan_validates_and_chains()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}" }));

        var app = plan.Msi(MsiBuilder.Create("App")
            .WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001")
            .WithPayload("App")
            .WithMainExecutable("App.exe"));

        var bundle = plan.Bundle(BundleBuilder.Create("App")
            .WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000002")
            .WithNetFramework(NetFrameworkVersion.NetFx48)
            .WithMsi(app, m => m.DisplayInternalUi = true));

        Assert.Empty(InstallerValidator.Validate(plan));
        Assert.Equal("AppSetup", bundle.ResolvedOutputFileName);
        Assert.Equal(2, bundle.Chain.Items.Count);
        Assert.Same(app, Assert.IsType<MsiChainItem>(bundle.Chain.Items[1]).Definition);
    }

    [Fact]
    public void Builder_without_create_is_rejected_by_the_plan()
    {
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new string[0]));
        Assert.Throws<ArgumentException>(() => plan.Msi(new MsiBuilder()));
    }
}
