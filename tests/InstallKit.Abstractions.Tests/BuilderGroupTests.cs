using System.Linq;
using Xunit;

namespace InstallKit.Tests;

/// <summary>The grouped builder methods (Content, Feature, Machine, ...): they only forward to the model, and keep what was added in order.</summary>
public class BuilderGroupTests
{
    [Fact]
    public void A_feature_owns_the_files_added_inside_it()
    {
        var msi = MsiBuilder.Create("App")
            .Content(c => c.Payload("App"))
            .Feature("Service", "Background service", f => f.Payload("Svc", "service"))
            .Feature("Tray", f => f.Payload("Agent", "agent").Directory("extras"), installedByDefault: false)
            .Content(c => c.Directory("docs", "docs"))
            .Build();

        Assert.Equal(new[] { "Service", "Tray" }, msi.Features.Select(f => f.Name));
        Assert.Equal(new[] { true, false }, msi.Features.Select(f => f.InstalledByDefault));
        Assert.Equal("Background service", msi.Features[0].Description);
        Assert.Null(msi.Features[1].Description);
        Assert.Equal(new string?[] { null, "Service", "Tray", "Tray", null }, msi.Payloads.Select(p => p.Feature));
    }

    [Fact]
    public void A_group_can_be_used_more_than_once_and_keeps_the_order()
    {
        var msi = MsiBuilder.Create("App")
            .Machine(m => m.Registry(RegistryHive.LocalMachine, @"Software\App", "A", "1"))
            .Shell(s => s.DesktopShortcut())
            .Machine(m => m.Registry(RegistryHive.LocalMachine, @"Software\App", "B", 2).PathEntry("[INSTALLDIR]bin"))
            .Build();

        Assert.Equal(new[] { "A", "B" }, msi.RegistryEntries.Select(r => r.Name));
        Assert.Equal(new[] { "PATH" }, msi.EnvironmentVariables.Select(v => v.Name));
        Assert.True(msi.Shortcuts.Desktop);
    }

    [Fact]
    public void Settings_and_lifecycle_fill_the_model()
    {
        var msi = MsiBuilder.Create("App")
            .Settings(s => s.Property("MODE", "client").Prompt("SERVER", "Server", "localhost", required: true))
            .Lifecycle(l => l.CloseApplication("App.exe", terminate: true).RunAfterInstall("App.exe", "--init"))
            .Build();

        Assert.Equal("client", msi.Properties["MODE"]);
        Assert.Equal("SERVER", Assert.Single(msi.PromptedProperties).Name);
        Assert.True(Assert.Single(msi.CloseApplications).Terminate);
        Assert.Equal("--init", Assert.Single(msi.Commands).Arguments);
    }

    [Fact]
    public void Appearance_sets_the_ui_and_the_branding()
    {
        var modern = MsiBuilder.Create("App").Appearance(a => a.Modern(chooseInstallDir: true).AccentColor("#112233").Logo("logo.png")).Build();
        Assert.Equal(InstallerUi.ModernInstallDir, modern.Ui);
        Assert.Equal(("#112233", "logo.png"), (modern.Branding.AccentColor, modern.Branding.LogoFile));

        Assert.Equal(InstallerUi.ModernWpf, MsiBuilder.Create("App").Appearance(a => a.Wpf()).Build().Ui);
        Assert.Equal(InstallerUi.FeatureTree, MsiBuilder.Create("App").Appearance(a => a.Classic(InstallerUi.FeatureTree)).Build().Ui);
    }

    [Fact]
    public void The_theme_defaults_to_dark_and_can_follow_the_windows_setting()
    {
        Assert.Equal(InstallerTheme.Dark, MsiBuilder.Create("App").Build().Branding.Theme);
        Assert.Equal(InstallerTheme.Light, MsiBuilder.Create("App").Appearance(a => a.Flat().Theme(InstallerTheme.Light)).Build().Branding.Theme);
        Assert.Equal(InstallerUi.ModernFlat, MsiBuilder.Create("App").Appearance(a => a.Flat()).Build().Ui);

        var auto = MsiBuilder.Create("App").Appearance(a => a.Flat().Theme(InstallerTheme.Auto)).Build();
        Assert.Equal(InstallerTheme.Auto, auto.Branding.Theme);

        var bundle = BundleBuilder.Create("Setup").Appearance(a => a.Theme(InstallerTheme.Auto)).Build();
        Assert.Equal(InstallerTheme.Auto, bundle.Branding.Theme);
    }

    [Fact]
    public void A_bundle_chains_items_in_the_order_the_groups_are_called()
    {
        var app = new MsiDefinition("App");
        var bundle = BundleBuilder.Create("Setup")
            .Prerequisites(p => p.NetFramework(NetFrameworkVersion.NetFx48))
            .Packages(k => k.Msi(app))
            .Appearance(a => a.Logo("logo.png"))
            .Build();

        Assert.IsType<NetFrameworkChainItem>(bundle.Chain.Items[0]);
        Assert.IsType<MsiChainItem>(bundle.Chain.Items[1]);
        Assert.Equal("logo.png", bundle.Branding.LogoFile);
    }
}
