using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InstallKit.WixSharp.Engine.Tests;

/// <summary>What the engine writes for a bundle, in particular for the prerequisite helpers.</summary>
public class BundleEngineTests
{
    static readonly string Hash = new string('a', 128);

    static string? A(XElement element, string name) => (string?)element.Attribute(name);

    static BundleBuilder Bundle() => BundleBuilder.Create("My App").WithUpgradeCode(Wxs.BundleCode);

    [Fact]
    public void An_embedded_prerequisite_is_a_package_with_a_source_file_and_a_detect_condition()
    {
        var workspace = new Wxs.Workspace();
        var installer = System.IO.Path.Combine(workspace.Payload, "Svc.exe");

        var wxs = Wxs.Bundle(workspace, Bundle().WithVcRedist(installer));

        var package = wxs.One("ExePackage");
        Assert.EndsWith("Svc.exe", A(package, "SourceFile"));
        Assert.Equal("InstallKitVcRedistX64 = 1", A(package, "DetectCondition"));
        Assert.Equal("/install /quiet /norestart", A(package, "InstallArguments"));
        Assert.Equal("yes", A(package, "Permanent"));
        Assert.Empty(package.Elements());
    }

    [Fact]
    public void A_downloaded_prerequisite_keeps_its_name_address_hash_and_size_on_the_payload_only()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Bundle(workspace, Bundle().WithWebView2(PrerequisiteInstaller.FromDownload("https://go.microsoft.com/fwlink/p/?LinkId=2124703", Hash, 1_800_000, "MicrosoftEdgeWebview2Setup.exe")));

        // WiX rejects a payload element on a package that also has any of these (WIX0372).
        var package = wxs.One("ExePackage");
        foreach (var forbidden in new[] { "SourceFile", "Name", "DownloadUrl", "Compressed" })
            Assert.Null(A(package, forbidden));

        var payload = package.Elements().Single();
        Assert.Equal("ExePackagePayload", payload.Name.LocalName);
        Assert.Equal(("MicrosoftEdgeWebview2Setup.exe", "https://go.microsoft.com/fwlink/p/?LinkId=2124703", "1800000"), (A(payload, "Name"), A(payload, "DownloadUrl"), A(payload, "Size")));
        Assert.Equal(Hash.ToUpperInvariant(), A(payload, "Hash"));
        Assert.Equal("/silent /install", A(package, "InstallArguments"));
    }

    [Fact]
    public void Registry_searches_follow_the_architecture_and_the_webview2_runtime_is_looked_up_for_machine_and_user()
    {
        var workspace = new Wxs.Workspace();
        var installer = System.IO.Path.Combine(workspace.Payload, "Svc.exe");

        var wxs = Wxs.Bundle(workspace, Bundle().WithVcRedist(installer).WithVcRedist(installer, PrerequisiteArchitecture.X86).WithWebView2(installer));

        var searches = wxs.All("RegistrySearch").ToDictionary(s => A(s, "Variable")!);
        var x64 = searches["InstallKitVcRedistX64"];
        Assert.Equal(("HKLM", @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", "Installed", "value", "always64"), (A(x64, "Root"), A(x64, "Key"), A(x64, "Value"), A(x64, "Result"), A(x64, "Bitness")));
        Assert.Equal(("always32", @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"), (A(searches["InstallKitVcRedistX86"], "Bitness"), A(searches["InstallKitVcRedistX86"], "Key")));
        Assert.Equal("HKLM", A(searches["InstallKitWebView2"], "Root"));
        Assert.Equal("HKCU", A(searches["InstallKitWebView2User"], "Root"));
        Assert.Equal(searches.Count, searches.Values.Select(s => A(s, "Id")).Distinct().Count());
    }

    [Fact]
    public void A_dotnet_runtime_is_found_by_a_dotnet_search_and_compared_by_its_major_version()
    {
        var workspace = new Wxs.Workspace();
        var installer = System.IO.Path.Combine(workspace.Payload, "Svc.exe");

        var wxs = Wxs.Bundle(workspace, Bundle().WithDotNetRuntime(DotNetRuntimeKind.WindowsDesktop, 8, installer).WithDotNetRuntime(DotNetRuntimeKind.AspNetCore, 8, installer, PrerequisiteArchitecture.Arm64));

        var searches = wxs.All("DotNetCoreSearch").ToDictionary(s => A(s, "Variable")!);
        Assert.Equal(("desktop", "x64", "8"), (A(searches["InstallKitDotNetWindowsDesktop8X64"], "RuntimeType"), A(searches["InstallKitDotNetWindowsDesktop8X64"], "Platform"), A(searches["InstallKitDotNetWindowsDesktop8X64"], "MajorVersion")));
        Assert.Equal(("aspnet", "arm64"), (A(searches["InstallKitDotNetAspNetCore8Arm64"], "RuntimeType"), A(searches["InstallKitDotNetAspNetCore8Arm64"], "Platform")));
        Assert.Contains(wxs.All("ExePackage"), p => A(p, "DetectCondition") == "InstallKitDotNetWindowsDesktop8X64 >= v8.0.0.0");
    }

    [Fact]
    public void A_bundle_chains_the_msi_after_its_prerequisites()
    {
        var workspace = new Wxs.Workspace();
        var plan = new InstallerPlan(workspace.Context);
        var msi = plan.Msi(workspace.App());
        var installer = System.IO.Path.Combine(workspace.Payload, "Svc.exe");

        var wxs = Wxs.Bundle(workspace, Bundle().WithVcRedist(installer).WithMsi(msi), msi);

        var chain = wxs.One("Chain").Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(new[] { "ExePackage", "MsiPackage" }, chain);
        Assert.Equal(Wxs.BundleCode, A(wxs.One("Bundle"), "UpgradeCode"));
    }
}
