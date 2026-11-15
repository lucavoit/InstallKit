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
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App")).WithMainExecutable("App.exe");
        return (plan, builder, docs);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Features_and_their_files_are_collected()
    {
        var msi = MsiBuilder.Create("App")
            .Feature("Documentation", "Help files", f => f.Directory("docs", "docs"))
            .Feature("Samples", f => f.Payload("Tools", "tools"), installedByDefault: false)
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
        var msi = MsiBuilder.Create("App").Content(c => c.Payload("App").Directory("docs")).Build();

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
        plan.Msi(builder.Feature("Documentation", f => f.Directory(docs, "docs")));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Complete")]
    public void Unusable_names_are_reported(string name)
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.Feature(name, f => f.Directory(docs, "docs")));

        Assert.Contains("IK240", Codes(plan));
    }

    [Fact]
    public void Duplicate_features_are_reported_even_when_only_punctuation_differs()
    {
        var (plan, builder, docs) = NewPlan();
        plan.Msi(builder.Feature("Sample files", f => f.Directory(docs, "docs")).Feature("Sample_files", f => f.Directory(docs, "more")));

        Assert.Contains("IK241", Codes(plan));
    }

    [Fact]
    public void Files_for_an_undeclared_feature_are_reported()
    {
        var (plan, builder, docs) = NewPlan();
        // The builder cannot do this (a feature owns its files); only a model assembled by hand can.
        plan.Msi(builder.WithPayloads(new[] { PayloadSource.FromDirectory(docs, "docs", "Documentation") }.Concat(new[] { PayloadSource.FromPayload("App") }).ToList()));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK242", error.Code);
        Assert.Contains("Feature(", error.Message);
    }

    [Fact]
    public void A_feature_without_files_is_reported()
    {
        var (plan, builder, _) = NewPlan();
        plan.Msi(builder.Feature("Empty", f => { }));

        Assert.Contains("IK243", Codes(plan));
    }

    [Fact]
    public void Files_that_must_always_be_installed_cannot_be_in_a_feature()
    {
        var (plan, _, _) = NewPlan();
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "Optional.exe"), "");
        plan.Msi(MsiBuilder.Create("Opt").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000009")
            .Feature("Extras", f => f.Directory(dir)).WithMainExecutable("Optional.exe"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK244", error.Code);
        Assert.Contains("Extras", error.Message);
    }
}
