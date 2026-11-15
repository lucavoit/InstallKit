using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class FeatureTests
{
    static (InstallerPlan Plan, MsiBuilder Builder, string Dir) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var docs = Directory.CreateDirectory(Path.Combine(dir, "docs")).FullName;
        File.WriteAllText(Path.Combine(docs, "readme.txt"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").WithPayload("App").WithMainExecutable("App.exe");
        return (plan, builder, docs);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Features_and_their_files_are_collected()
    {
        var msi = MsiBuilder.Create("App")
            .WithFeature("Documentation", "Help files")
            .WithFeature("Samples", installedByDefault: false)
            .WithDirectory("docs", "docs", feature: "Documentation")
            .WithPayload("Tools", "tools", "Samples")
            .Build();

        Assert.Equal(2, msi.Features.Count);
        Assert.True(msi.Features[0].InstalledByDefault);
        Assert.False(msi.Features[1].InstalledByDefault);
        Assert.Equal("Help files", msi.Features[0].Description);
        Assert.Equal(new string?[] { "Documentation", "Samples" }, msi.Payloads.Select(p => p.Feature));
    }

    [Fact]
    public void Payloads_without_a_feature_are_always_installed()
    {
        var msi = MsiBuilder.Create("App").WithPayload("App").WithDirectory("docs").Build();

        Assert.All(msi.Payloads, p => Assert.Null(p.Feature));
        Assert.Single(new MsiDefinition("App").AddFeature("Docs").AddPayload("App", null, "Docs").Features);
    }

    [Theory]
    [InlineData("Sample files", "Sample_files")]
    [InlineData("1st", "_1st")]
    [InlineData("a-b", "a_b")]
    [InlineData("", "_")]
    public void The_id_is_a_safe_msi_identifier(string name, string id) => Assert.Equal(id, new OptionalFeature(name).Id);

    [Fact]
    public void A_declared_feature_with_files_is_valid()
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.WithFeature("Documentation").WithDirectory(docs, "docs", "Documentation"));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Complete")]
    public void Unusable_names_are_reported(string name)
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.WithFeature(name).WithDirectory(docs, "docs", name));

        Assert.Contains("IK240", Codes(plan));
    }

    [Fact]
    public void Duplicate_features_are_reported_even_when_only_punctuation_differs()
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.WithFeature("Sample files").WithFeature("Sample_files").WithDirectory(docs, "docs", "Sample files"));

        Assert.Contains("IK241", Codes(plan));
    }

    [Fact]
    public void Files_for_an_undeclared_feature_are_reported()
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.WithDirectory(docs, "docs", "Documentation"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK242", error.Code);
        Assert.Contains("WithFeature", error.Message);
    }

    [Fact]
    public void A_feature_without_files_is_reported()
    {
        var (plan, builder, _) = NewPlan();
        plan.Msi(builder.WithFeature("Empty"));

        Assert.Contains("IK243", Codes(plan));
    }

    [Fact]
    public void Files_that_must_always_be_installed_cannot_be_in_a_feature()
    {
        var (plan, _, _) = NewPlan();
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "Optional.exe"), "");
        plan.Msi(MsiBuilder.Create("Opt").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000009")
            .WithFeature("Extras").WithDirectory(dir, feature: "Extras").WithMainExecutable("Optional.exe"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK244", error.Code);
        Assert.Contains("Extras", error.Message);
    }

    static (InstallerPlan Plan, MsiBuilder Builder, string Folder) PlanWithFeatureFiles()
    {
        var (plan, builder, _) = NewPlan();
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "Worker.exe"), "");
        File.WriteAllText(Path.Combine(dir, "worker.config"), "<configuration/>");
        return (plan, builder.WithFeature("Worker").WithDirectory(dir, "worker", "Worker"), dir);
    }

    [Fact]
    public void A_service_can_live_in_a_feature()
    {
        var (plan, builder, _) = PlanWithFeatureFiles();
        plan.Msi(builder.WithService(@"worker\Worker.exe", "WorkerSvc"));

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void An_xml_value_can_live_in_a_feature()
    {
        var (plan, builder, _) = PlanWithFeatureFiles();
        plan.Msi(builder.WithXmlValue(@"worker\worker.config", "/configuration/@x", "1"));

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void A_file_association_a_firewall_rule_and_the_main_executable_still_need_an_always_installed_file()
    {
        var (planA, builderA, _) = PlanWithFeatureFiles();
        planA.Msi(builderA.WithFileAssociation(".wrk", "Worker file", @"worker\Worker.exe"));
        Assert.Contains("IK244", Codes(planA));

        var (planB, builderB, _) = PlanWithFeatureFiles();
        planB.Msi(builderB.WithFirewallRule("Worker", executable: @"worker\Worker.exe", protocol: FirewallProtocol.Any));
        Assert.Contains("IK244", Codes(planB));

        var (planC, builderC, _) = PlanWithFeatureFiles();
        planC.Msi(builderC.WithMainExecutable(@"worker\Worker.exe"));
        Assert.Contains("IK244", Codes(planC));
    }

    [Fact]
    public void The_message_says_what_is_allowed()
    {
        var (plan, builder, _) = PlanWithFeatureFiles();
        plan.Msi(builder.WithMainExecutable(@"worker\Worker.exe"));

        var message = InstallerValidator.Validate(plan).Single(e => e.Code == "IK244").Message;
        Assert.Contains("Only services and XML values may live in a feature", message);
    }
}
