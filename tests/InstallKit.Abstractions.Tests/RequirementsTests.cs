using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class RequirementsTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan(string? depsJson = null)
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        if (depsJson != null)
            File.WriteAllText(Path.Combine(dir, "App.deps.json"), depsJson);
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App"));
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    static string Deps(string rid) => "{ \"runtimeTarget\": { \"name\": \".NETCoreApp,Version=v8.0/" + rid + "\", \"signature\": \"\" } }";

    [Fact]
    public void Helpers_add_conditions_that_do_not_block_removal()
    {
        var msi = MsiBuilder.Create("App")
            .Requirements(r => r.Requires64Bit().RequiresWindows(19041).LaunchCondition("MYPROP = 1", "Set MYPROP."))
            .Build();

        Assert.Equal(new[] { "Installed OR (VersionNT64)", "Installed OR (WindowsBuild >= 19041)", "MYPROP = 1" }, msi.LaunchConditions.Select(c => c.Condition));
        Assert.All(msi.LaunchConditions, c => Assert.False(string.IsNullOrWhiteSpace(c.Message)));
        Assert.Single(new MsiDefinition("App").AddLaunchCondition("VersionNT64", "64-bit").LaunchConditions);
    }

    [Fact]
    public void Empty_conditions_and_messages_are_rejected()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Requirements(r => r.LaunchCondition("", "x").LaunchCondition("VersionNT64", " ")));

        Assert.Equal(new[] { "IK320", "IK321" }, Codes(plan));
    }

    [Fact]
    public void A_payload_for_another_platform_than_the_package_is_reported()
    {
        var (plan, builder) = NewPlan(Deps("win-x86"));
        plan.Msi(builder); // x64 package

        var warning = Assert.Single(PlatformChecks.Warnings(plan));
        Assert.Contains("IK122", warning);
        Assert.Contains("win-x86", warning);
        Assert.Contains("InstallerPlatform.X86", warning);
    }

    [Fact]
    public void An_x86_package_with_x64_files_explains_the_consequences()
    {
        var (plan, builder) = NewPlan(Deps("win-x64"));
        plan.Msi(builder.WithPlatform(InstallerPlatform.X86));

        var warning = Assert.Single(PlatformChecks.Warnings(plan));
        Assert.Contains("Program Files (x86)", warning);
    }

    [Fact]
    public void Matching_or_unknown_payloads_give_no_warning()
    {
        var (matching, b1) = NewPlan(Deps("win-x64"));
        matching.Msi(b1);
        var (unknown, b2) = NewPlan();
        unknown.Msi(b2);

        Assert.Empty(PlatformChecks.Warnings(matching));
        Assert.Empty(PlatformChecks.Warnings(unknown));
        Assert.Null(PlatformChecks.DetectRuntimeIdentifier("/does/not/exist"));
    }

    [Fact]
    public void Data_folders_are_validated()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m
            .DataFolder(System.Environment.SpecialFolder.CommonApplicationData, @"Contoso\App", f => f.Allow("SYSTEM", FolderRights.FullControl).Allow("system", FolderRights.Read))
            .DataFolder(System.Environment.SpecialFolder.CommonApplicationData, @"..\Escape", f => f.Allow("Users", FolderRights.Read))
            .DataFolder(System.Environment.SpecialFolder.ApplicationData, @"Contoso\Other", f => f.Allow("Users", FolderRights.Read))
            .DataFolder(System.Environment.SpecialFolder.CommonApplicationData, @"Contoso\Empty")));

        Assert.Equal(new[] { "IK334", "IK331", "IK330", "IK332" }, Codes(plan));
    }

    [Fact]
    public void A_valid_data_folder_passes()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.DataFolder(System.Environment.SpecialFolder.CommonApplicationData, "Contoso/App", f => f
            .Allow("SYSTEM", FolderRights.FullControl).Allow("Users", FolderRights.Read))));

        Assert.Empty(Codes(plan));
        Assert.Equal(@"Contoso\App", plan.Msis[0].DataFolders[0].Path);
    }

    [Theory]
    [InlineData(NetFrameworkVersion.NetFx462, 394802)]
    [InlineData(NetFrameworkVersion.NetFx472, 461808)]
    [InlineData(NetFrameworkVersion.NetFx48, 528040)]
    [InlineData(NetFrameworkVersion.NetFx481, 533320)]
    public void The_dotnet_framework_helper_compares_the_release_number(NetFrameworkVersion version, int release)
    {
        var msi = MsiBuilder.Create("App").Requirements(r => r.RequiresNetFramework(version)).Build();

        var condition = Assert.Single(msi.LaunchConditions);
        Assert.Equal($"Installed OR (INSTALLKIT_NETFX_RELEASE >= \"#{release}\")", condition.Condition);
        Assert.Contains("App", condition.Message);
        var search = Assert.Single(msi.PropertySearches);
        Assert.Equal(("INSTALLKIT_NETFX_RELEASE", "Release", true), (search.Property, search.ValueName, search.Win64));
    }

    [Fact]
    public void The_webview2_helper_checks_the_machine_and_the_user()
    {
        var msi = MsiBuilder.Create("App").Requirements(r => r.RequiresWebView2("Install WebView2.")).Build();

        Assert.Equal(new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }, msi.PropertySearches.Select(s => s.Hive));
        Assert.All(msi.PropertySearches, s => Assert.False(s.Win64));
        var condition = Assert.Single(msi.LaunchConditions);
        Assert.Equal("Install WebView2.", condition.Message);
        Assert.StartsWith("Installed OR (", condition.Condition);
        Assert.Contains("INSTALLKIT_WEBVIEW2_USER <> \"0.0.0.0\"", condition.Condition);
    }

    [Fact]
    public void Asking_twice_searches_the_registry_once()
    {
        var msi = MsiBuilder.Create("App")
            .Requirements(r => r.RequiresNetFramework(NetFrameworkVersion.NetFx472).RequiresNetFramework(NetFrameworkVersion.NetFx48))
            .Build();

        Assert.Single(msi.PropertySearches);
        Assert.Equal(2, msi.LaunchConditions.Count);
    }
}
