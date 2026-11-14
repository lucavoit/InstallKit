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
}
