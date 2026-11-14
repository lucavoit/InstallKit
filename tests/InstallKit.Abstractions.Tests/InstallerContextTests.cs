using System;
using System.IO;
using Xunit;

namespace InstallKit.Tests;

public class InstallerContextTests
{
    [Fact]
    public void Command_line_values_are_parsed()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var context = InstallerContext.FromEnvironment(new[]
        {
            "--version", "2.3.4-rc.1",
            "--output", dir,
            "--payload", $"App={dir}",
            "--payload=Tools=" + dir,
            "--configuration", "Debug",
        });

        Assert.Equal(new Version(2, 3, 4, 0), context.Version);
        Assert.Equal(Path.GetFullPath(dir), context.OutputDirectory);
        Assert.Equal(Path.GetFullPath(dir), context.TryGetPayloadDirectory("app"));
        Assert.Equal(Path.GetFullPath(dir), context.TryGetPayloadDirectory("Tools"));
        Assert.True(context.IsDebug);
        Assert.Null(context.Signer);
    }

    [Fact]
    public void Payload_manifest_is_read()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var manifest = Path.Combine(dir, "payloads.txt");
        File.WriteAllLines(manifest, new[] { $"MyApp={dir}", "garbage", "" });

        var context = InstallerContext.FromEnvironment(new[] { "--payload-manifest", manifest });

        Assert.Equal(Path.GetFullPath(dir), context.TryGetPayloadDirectory("MyApp"));
        Assert.Single(context.Payloads);
    }

    [Fact]
    public void Invalid_version_is_a_validation_error()
    {
        var ex = Assert.Throws<InstallerValidationException>(() => InstallerContext.FromEnvironment(new[] { "--version", "1.2.99999" }));
        Assert.Equal("IK001", ex.Errors[0].Code);
    }

    [Fact]
    public void Wix_eula_acceptance_is_passed_through()
    {
        Assert.Null(InstallerContext.FromEnvironment(new string[0]).AcceptedWixEula);
        Assert.Equal("wix7", InstallerContext.FromEnvironment(new[] { "--accept-wix-eula", "wix7" }).AcceptedWixEula);
    }

    [Fact]
    public void Sign_command_needs_placeholder()
    {
        Assert.Throws<ArgumentException>(() => new CommandFileSigner("signtool sign"));
        Assert.IsType<CommandFileSigner>(InstallerContext.FromEnvironment(new[] { "--sign-command", "signtool sign {file}" }).Signer);
    }
}
