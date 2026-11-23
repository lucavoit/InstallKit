using System;
using Xunit;

namespace InstallKit.Tests;

public class ProductVersionTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3.0")]
    [InlineData("v1.2.3", "1.2.3.0")]
    [InlineData("1.2.3-beta.4+abc", "1.2.3.0")]
    [InlineData("1.2", "1.2.0.0")]
    [InlineData("7", "7.0.0.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("255.255.65535", "255.255.65535.0")]
    public void Normalises_valid_versions(string input, string expected)
    {
        Assert.True(ProductVersion.TryParse(input, out var version, out var error));
        Assert.Null(error);
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("256.0.0")]
    [InlineData("1.2.20241114")]
    public void Rejects_versions_msi_cannot_use(string input)
    {
        Assert.False(ProductVersion.TryParse(input, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("1.2.3", null)]
    [InlineData("v1.2.3", null)]
    [InlineData("1.2.3-rc1", "-rc1")]
    [InlineData("1.2.3-beta.4+sha", "-beta.4+sha")]
    [InlineData("1.2.3+build5", "+build5")]
    [InlineData("1.2.3-", null)]
    [InlineData("", null)]
    public void Reports_the_suffix_that_is_dropped(string input, string? expected)
        => Assert.Equal(expected, ProductVersion.GetDroppedSuffix(input));

    [Fact]
    public void Context_warns_and_keeps_the_suffix_when_it_is_dropped()
    {
        var output = new System.IO.StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var context = InstallerContext.FromEnvironment(new[] { "--version", "1.2.3-rc1" });
            Assert.Equal("-rc1", context.VersionSuffix);
            Assert.Equal(new Version(1, 2, 3, 0), context.Version);
        }
        finally { Console.SetOut(original); }

        Assert.Contains("IK310", output.ToString());
    }
}
