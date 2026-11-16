using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class PrerequisiteTests
{
    static readonly string Hash = new string('a', 128);

    static (InstallerPlan Plan, string Dir) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        File.WriteAllText(Path.Combine(dir, "vc_redist.x64.exe"), "");
        File.WriteAllText(Path.Combine(dir, "dotnet.exe"), "");
        File.WriteAllText(Path.Combine(dir, "webview2.exe"), "");
        return (new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir })), dir);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    static BundleBuilder Bundle() => BundleBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000002");

    [Fact]
    public void A_vc_redist_from_a_file_adds_a_registry_search_and_a_detect_condition()
    {
        var bundle = Bundle().Prerequisites(p => p.VcRedist("vc_redist.x64.exe")).Build();

        var item = Assert.IsType<ExeChainItem>(Assert.Single(bundle.Chain.Items));
        Assert.Equal("vc_redist.x64.exe", item.Path);
        Assert.Null(item.Download);
        Assert.Equal("/install /quiet /norestart", item.InstallArguments);
        Assert.Equal("InstallKitVcRedistX64 = 1", item.DetectCondition);

        var search = Assert.IsType<RegistryBundleSearch>(Assert.Single(bundle.Chain.Searches));
        Assert.Equal(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", search.Key);
        Assert.Equal("Installed", search.Value);
        Assert.True(search.Win64);
    }

    [Theory]
    [InlineData(PrerequisiteArchitecture.X86, "x86", false)]
    [InlineData(PrerequisiteArchitecture.Arm64, "arm64", true)]
    public void The_vc_redist_registry_view_follows_the_architecture(PrerequisiteArchitecture architecture, string folder, bool win64)
    {
        var search = Assert.IsType<RegistryBundleSearch>(Assert.Single(Bundle().Prerequisites(p => p.VcRedist("a.exe", architecture)).Build().Chain.Searches));

        Assert.EndsWith(@"Runtimes\" + folder, search.Key);
        Assert.Equal(win64, search.Win64);
    }

    [Fact]
    public void A_dotnet_runtime_adds_a_dotnet_search_and_compares_the_major_version()
    {
        var bundle = Bundle().Prerequisites(p => p.DotNetRuntime(DotNetRuntimeKind.WindowsDesktop, 8, "dotnet.exe", PrerequisiteArchitecture.X64)).Build();

        var search = Assert.IsType<DotNetBundleSearch>(Assert.Single(bundle.Chain.Searches));
        Assert.Equal((DotNetRuntimeKind.WindowsDesktop, 8, PrerequisiteArchitecture.X64), (search.Kind, search.MajorVersion, search.Architecture));
        Assert.Equal("InstallKitDotNetWindowsDesktop8X64", search.Variable);
        Assert.Equal("InstallKitDotNetWindowsDesktop8X64 >= v8.0.0.0", Assert.IsType<ExeChainItem>(Assert.Single(bundle.Chain.Items)).DetectCondition);
    }

    [Fact]
    public void WebView2_looks_in_the_machine_and_the_user_registry()
    {
        var bundle = Bundle().Prerequisites(p => p.WebView2("webview2.exe")).Build();

        var searches = bundle.Chain.Searches.Cast<RegistryBundleSearch>().ToList();
        Assert.Equal(new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }, searches.Select(s => s.Root));
        Assert.All(searches, s => Assert.Contains("F3017226-FE2A-4295-8BDF-00C3A9A7E4C5", s.Key));
        Assert.Equal("/silent /install", Assert.IsType<ExeChainItem>(Assert.Single(bundle.Chain.Items)).InstallArguments);
        var condition = Assert.IsType<ExeChainItem>(bundle.Chain.Items[0]).DetectCondition!;
        Assert.Contains("InstallKitWebView2 <> \"\"", condition);
        Assert.Contains("InstallKitWebView2User <> \"\"", condition);
    }

    [Fact]
    public void A_download_is_described_by_url_hash_and_size()
    {
        var installer = PrerequisiteInstaller.FromDownload("https://aka.ms/vs/17/release/vc_redist.x64.exe", Hash, 25_000_000);

        var item = Assert.IsType<ExeChainItem>(Assert.Single(Bundle().Prerequisites(p => p.VcRedist(installer)).Build().Chain.Items));
        Assert.Same(installer, item.Download);
        Assert.Equal("vc_redist.x64.exe", item.Path);
        Assert.Equal("setup.exe", PrerequisiteInstaller.FromDownload("https://x.example/dl/setup.exe?v=1", Hash, 1).ResolvedFileName);
        Assert.Equal("mine.exe", PrerequisiteInstaller.FromDownload("https://x.example/", Hash, 1, "mine.exe").ResolvedFileName);
    }

    [Fact]
    public void Valid_prerequisites_pass_validation()
    {
        var (plan, dir) = NewPlan();
        plan.Bundle(Bundle()
            .Prerequisites(p => p
                .VcRedist(Path.Combine(dir, "vc_redist.x64.exe"))
                .VcRedist(PrerequisiteInstaller.FromDownload("https://aka.ms/vs/17/release/vc_redist.x86.exe", Hash, 13_000_000), PrerequisiteArchitecture.X86)
                .DotNetRuntime(DotNetRuntimeKind.Runtime, 8, Path.Combine(dir, "dotnet.exe"))
                .WebView2(Path.Combine(dir, "webview2.exe")))
            .Packages(k => k.Msi(plan.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App"))))));

        Assert.Empty(Codes(plan));
    }

    [Fact]
    public void An_embedded_prerequisite_must_exist()
    {
        var (plan, dir) = NewPlan();
        plan.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App")));
        plan.Bundle(Bundle().Prerequisites(p => p.VcRedist(Path.Combine(dir, "missing.exe"))));

        Assert.Contains("IK133", Codes(plan));
    }

    [Theory]
    [InlineData("http://example.com/a.exe", "IK260")]
    [InlineData("not a url", "IK260")]
    public void A_download_must_use_https(string url, string code)
    {
        var (plan, _) = NewPlan();
        plan.Bundle(Bundle().Prerequisites(p => p.VcRedist(PrerequisiteInstaller.FromDownload(url, Hash, 10, "a.exe"))));

        Assert.Contains(code, Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzz")]
    public void A_download_needs_a_sha512_hash(string hash)
    {
        var (plan, _) = NewPlan();
        plan.Bundle(Bundle().Prerequisites(p => p.WebView2(PrerequisiteInstaller.FromDownload("https://x.example/a.exe", hash, 10))));

        Assert.Contains("IK261", Codes(plan));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(3_000_000_000)]
    public void A_download_needs_a_sane_size(long size)
    {
        var (plan, _) = NewPlan();
        plan.Bundle(Bundle().Prerequisites(p => p.WebView2(PrerequisiteInstaller.FromDownload("https://x.example/a.exe", Hash, size))));

        Assert.Contains("IK262", Codes(plan));
    }

    [Theory]
    [InlineData("setup.msi")]
    [InlineData(@"sub\a.exe")]
    public void A_download_needs_a_usable_file_name(string name)
    {
        var (plan, _) = NewPlan();
        plan.Bundle(Bundle().Prerequisites(p => p.WebView2(PrerequisiteInstaller.FromDownload("https://x.example/a.exe", Hash, 10, name))));

        Assert.Contains("IK263", Codes(plan));
    }

    [Fact]
    public void Using_a_helper_twice_for_the_same_runtime_is_reported()
    {
        var (plan, dir) = NewPlan();
        plan.Msi(MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App")));
        plan.Bundle(Bundle().Prerequisites(p => p.VcRedist(Path.Combine(dir, "vc_redist.x64.exe")).VcRedist(Path.Combine(dir, "vc_redist.x64.exe"))));

        Assert.Contains("IK264", Codes(plan));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void The_dotnet_major_version_must_be_plausible(int major)
    {
        var (plan, dir) = NewPlan();
        plan.Bundle(Bundle().Prerequisites(p => p.DotNetRuntime(DotNetRuntimeKind.Runtime, major, Path.Combine(dir, "dotnet.exe"))));

        Assert.Contains("IK265", Codes(plan));
    }
}
