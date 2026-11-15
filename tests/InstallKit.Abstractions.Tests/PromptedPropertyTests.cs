using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class PromptedPropertyTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App"));
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Builder_and_property_style_collect_prompts()
    {
        var msi = MsiBuilder.Create("App")
            .Settings(s => s
                .Prompt("SERVER", "Server address", "localhost", required: true)
                .Prompt("PORT", "Port"))
            .Build();

        Assert.Equal(("SERVER", "Server address", "localhost", true), (msi.PromptedProperties[0].Name, msi.PromptedProperties[0].Label, msi.PromptedProperties[0].DefaultValue, msi.PromptedProperties[0].Required));
        Assert.Null(msi.PromptedProperties[1].DefaultValue);
        Assert.False(msi.PromptedProperties[1].Required);
        Assert.Single(new MsiDefinition("App").AddPromptedProperty("A", "a").PromptedProperties);
        Assert.Null(new PromptedProperty("A", "a", "").DefaultValue);
    }

    [Fact]
    public void Valid_prompts_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt("SERVER", "Server", "x").Prompt("DB_NAME", "Database").Prompt("_P2", "Other", required: true)));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("server")]
    [InlineData("1SERVER")]
    [InlineData("MY-PROP")]
    [InlineData("")]
    [InlineData("A B")]
    public void A_property_name_must_be_upper_case_with_digits_and_underscores(string name)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt(name, "Label")));

        Assert.Contains("IK280", Codes(plan));
    }

    [Fact]
    public void A_name_defined_twice_or_also_set_as_a_property_is_reported()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt("SERVER", "a").Prompt("SERVER", "b")));
        Assert.Contains("IK281", Codes(plan));

        var (plan2, builder2) = NewPlan();
        plan2.Msi(builder2.Settings(s => s.Property("MODE", "x").Prompt("MODE", "Mode")));
        Assert.Contains("IK281", Codes(plan2));
    }

    [Fact]
    public void A_prompt_needs_a_label()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt("SERVER", " ")));

        Assert.Contains("IK282", Codes(plan));
    }

    [Fact]
    public void Only_three_prompts_fit_on_the_page()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt("A", "a").Prompt("B", "b").Prompt("C", "c").Prompt("D", "d")));

        Assert.Contains("IK283", Codes(plan));
    }

    [Theory]
    [InlineData("INSTALLDIR")]
    [InlineData("ALLUSERS")]
    [InlineData("ADDLOCAL")]
    public void Properties_that_windows_installer_uses_are_reserved(string name)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Settings(s => s.Prompt(name, "Label")));

        Assert.Contains("IK284", Codes(plan));
    }
}
