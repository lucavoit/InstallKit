using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class XmlValueTests
{
    const string ServerXPath = "/configuration/appSettings/add[@key='Server']/@value";

    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe.config"), "<configuration/>");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{}");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").WithPayload("App");
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Builder_and_property_style_collect_values()
    {
        var msi = MsiBuilder.Create("App").WithXmlValue("App.exe.config", ServerXPath, "[SERVER]").Build();
        Assert.Equal(("App.exe.config", ServerXPath, "[SERVER]"), (msi.XmlValues[0].File, msi.XmlValues[0].XPath, msi.XmlValues[0].Value));

        Assert.Single(new MsiDefinition("App").AddXmlValue("a.config", "/a/@b", "c").XmlValues);
    }

    [Fact]
    public void Brackets_in_the_xpath_are_escaped_for_msi_but_not_in_the_value()
    {
        var edit = new XmlValue("App.exe.config", ServerXPath, "[SERVER]");

        Assert.Equal("/configuration/appSettings/add[\\[]@key='Server'[\\]]/@value", edit.EscapedXPath);
        Assert.Equal("[SERVER]", edit.Value);
        Assert.Equal("/a/b", new XmlValue("f.xml", "/a/b", "x").EscapedXPath);
    }

    [Fact]
    public void A_valid_value_in_an_installed_file_passes()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithXmlValue("App.exe.config", ServerXPath, "[SERVER]").WithXmlValue("App.exe.config", "/configuration/@x", ""));

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void The_file_must_be_installed()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithXmlValue("Missing.config", ServerXPath, "x"));

        Assert.Contains("IK121", Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/configuration/[")]
    public void A_bad_xpath_is_reported(string xpath)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithXmlValue("App.exe.config", xpath, "x"));

        Assert.Contains("IK221", Codes(plan));
    }

    [Fact]
    public void A_missing_file_name_is_reported()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithXmlValue("", "/a/@b", "x"));

        Assert.Contains("IK220", Codes(plan));
    }

    [Fact]
    public void Json_files_are_not_supported_yet()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithXmlValue("settings.json", "/a/@b", "x"));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK222", error.Code);
        Assert.Contains("JSON", error.Message);
    }
}
