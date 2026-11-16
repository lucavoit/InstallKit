using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class FirewallTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "Svc.exe"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App"));
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Builder_collects_rules_with_defaults()
    {
        var msi = MsiBuilder.Create("App")
            .Machine(m => m
                .FirewallRule("App web", "8080")
                .FirewallRule("App service", executable: "Svc.exe", protocol: FirewallProtocol.Any, scope: FirewallScope.LocalSubnet, profile: FirewallProfile.Private))
            .Build();

        Assert.Equal(2, msi.FirewallRules.Count);
        var web = msi.FirewallRules[0];
        Assert.Equal(("8080", FirewallProtocol.Tcp, FirewallScope.Any, FirewallProfile.All), (web.Port, web.Protocol, web.Scope, web.Profile));
        Assert.Null(web.Executable);
        Assert.Equal(FirewallScope.LocalSubnet, msi.FirewallRules[1].Scope);
        Assert.Equal(FirewallProfile.Private, msi.FirewallRules[1].Profile);
    }

    [Fact]
    public void Property_style_api_matches_the_builder() =>
        Assert.Equal("80,443", new MsiDefinition("App").AddFirewallRule("Web", "80,443").FirewallRules.Single().Port);

    [Theory]
    [InlineData("8080")]
    [InlineData("8000-8100")]
    [InlineData("80,443, 8000-8100")]
    [InlineData("65535")]
    public void Valid_ports_pass(string port)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("Web", port, FirewallProtocol.Udp)));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("http")]
    [InlineData("9000-8000")]
    [InlineData("1-2-3")]
    [InlineData("80,")]
    public void Invalid_ports_are_reported(string port)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("Web", port)));

        Assert.Contains("IK212", Codes(plan));
    }

    [Fact]
    public void A_rule_needs_a_port_or_a_program()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("Nothing")));

        Assert.Contains("IK211", Codes(plan));
    }

    [Fact]
    public void A_rule_needs_a_name()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("", "80")));

        Assert.Contains("IK210", Codes(plan));
    }

    [Fact]
    public void A_port_needs_tcp_or_udp()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("Web", "80", FirewallProtocol.Any)));

        Assert.Contains("IK213", Codes(plan));
    }

    [Fact]
    public void A_program_rule_checks_the_program_is_installed()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FirewallRule("Ok", executable: "Svc.exe", protocol: FirewallProtocol.Any).FirewallRule("Missing", executable: "Nope.exe", protocol: FirewallProtocol.Any)));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK121", error.Code);
        Assert.Contains("Nope.exe", error.Message);
    }

    [Fact]
    public void Per_user_installs_cannot_open_the_firewall()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.WithScope(InstallScope.PerUser).Machine(m => m.FirewallRule("Web", "80")));

        Assert.Contains("IK214", Codes(plan));
    }
}
