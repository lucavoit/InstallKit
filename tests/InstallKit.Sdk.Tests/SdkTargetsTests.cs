using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace InstallKit.Sdk.Tests;

public class SdkTargetsTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_root, true);

    static string SdkDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "InstallKit.Sdk", "Sdk");
            if (File.Exists(Path.Combine(candidate, "Sdk.targets")))
                return candidate;
        }

        throw new InvalidOperationException("src/InstallKit.Sdk/Sdk was not found above the test binaries.");
    }

    /// <summary>
    /// A project that uses the SDK's props and targets. The nested Microsoft.NET.Sdk imports are removed, because the
    /// project itself uses that SDK (the real SDK package is not restored here).
    /// </summary>
    string Project(string folder)
    {
        var copy = Path.Combine(_root, "sdk");
        Directory.CreateDirectory(copy);
        foreach (var name in new[] { "Sdk.props", "Sdk.targets" })
        {
            var text = File.ReadAllText(Path.Combine(SdkDirectory(), name));
            text = Regex.Replace(text, @"^\s*<Import Project=""Sdk\.(props|targets)"" Sdk=""Microsoft\.NET\.Sdk""\s*/>\s*$", "", RegexOptions.Multiline);
            File.WriteAllText(Path.Combine(copy, name), text);
        }

        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var project = Path.Combine(dir, "p.csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="{Path.Combine(copy, "Sdk.props")}" />
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup><InstallerPayload Include="x.csproj" /></ItemGroup>
              <Import Project="{Path.Combine(copy, "Sdk.targets")}" />
            </Project>
            """);
        return project;
    }

    static (int Exit, string Output) Msbuild(string project, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(project)! };
        foreach (var argument in new[] { "msbuild", project, "-nologo" })
            start.ArgumentList.Add(argument);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    string Property(string project, string name, params string[] extra)
    {
        var (exit, output) = Msbuild(project, [.. extra, $"-getProperty:{name}"]);
        Assert.True(exit == 0, output);
        return output.Trim();
    }

    [Fact]
    public void Building_the_installer_on_every_build_is_opt_in()
    {
        var project = Project("a");

        Assert.Equal("false", Property(project, "InstallKitBuildOnBuild"));
        Assert.Equal("true", Property(project, "InstallKitBuildOnBuild", "-p:InstallKitBuildOnBuild=true"));
    }

    [Fact]
    public void The_wix_version_is_read_from_a_manifest_with_comments_and_trailing_commas()
    {
        var project = Project("repo/proj");
        Directory.CreateDirectory(Path.Combine(_root, "repo", ".config"));
        File.WriteAllText(Path.Combine(_root, "repo", ".config", "dotnet-tools.json"), """
            {
              // pinned tools
              "version": 1,
              "tools": {
                "other": { "version": "1.0.0", "commands": ["x"] },
                "wix": {
                  "commands": ["wix"],
                  "version": "6.0.2",
                },
              },
            }
            """);

        Assert.Equal("6.0.2", Property(project, "InstallKitWixVersion", "-t:InstallKitResolveWix"));
        Assert.Equal("6", Property(project, "_InstallKitWixMajor", "-t:InstallKitResolveWix"));
    }

    [Fact]
    public void A_manifest_outside_the_default_place_can_be_named()
    {
        var project = Project("custom");
        File.WriteAllText(Path.Combine(_root, "tools.json"), """{ "tools": { "wix": { "version": "7.0.1" } } }""");

        Assert.Equal("7.0.1", Property(project, "InstallKitWixVersion", "-t:InstallKitResolveWix", $"-p:InstallKitToolManifest={Path.Combine(_root, "tools.json")}"));
    }

    [Fact]
    public void A_manifest_in_the_root_is_used_with_a_warning()
    {
        var project = Project("rootmanifest/proj");
        File.WriteAllText(Path.Combine(_root, "rootmanifest", "dotnet-tools.json"), """{ "tools": { "wix": { "version": "5.0.2" } } }""");

        var (exit, output) = Msbuild(project, "-t:InstallKitResolveWix", "-getProperty:InstallKitWixVersion");

        Assert.Equal(0, exit);
        Assert.Contains("5.0.2", output);
        Assert.Contains("IK002", output);
    }

    [Theory]
    [InlineData("0.0.70000")]
    [InlineData("256.0.0")]
    [InlineData("20241114.1.2")]
    public void A_version_that_does_not_fit_an_msi_version_fails_early(string version)
    {
        var project = Project("v");

        var (exit, output) = Msbuild(project, "-t:_InstallKitCheckVersion", $"-p:InstallKitVersion={version}");

        Assert.NotEqual(0, exit);
        Assert.Contains("IK311", output);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3-rc1")]
    [InlineData("255.255.65535")]
    [InlineData("1.2")]
    [InlineData("1.2.0.99999")]
    public void Versions_that_fit_pass(string version)
    {
        var project = Project("ok");

        var (exit, output) = Msbuild(project, "-t:_InstallKitCheckVersion", $"-p:InstallKitVersion={version}");

        Assert.True(exit == 0, output);
        Assert.DoesNotContain("IK311", output);
    }
}
