using System.Linq;
using System.Xml.Linq;
using InstallKit.WixSharp;
using Xunit;

namespace InstallKit.WixSharp.Tests;

public class BundleThemeTests
{
    static readonly XNamespace Ns = "http://wixtoolset.org/schemas/v4/thmutil";

    static XElement Build(bool light = false, string? background = null, string? text = null, BundleTheme.LicenseStyle license = BundleTheme.LicenseStyle.Rtf) =>
        BundleTheme.Build(Palette.Create(light, "#0067C0", background, text), license).Root!;

    static string? Named(XElement theme, string name) =>
        theme.Descendants().Where(e => (string?)e.Attribute("Name") == name).Select(e => e.Name.LocalName).FirstOrDefault();

    [Fact]
    public void Dark_is_the_default_and_every_font_paints_on_the_background()
    {
        var fonts = Build().Elements(Ns + "Font").ToList();

        Assert.All(fonts.Take(4), f => Assert.Equal("0F1116", (string)f.Attribute("Background")!));
        Assert.Equal("F3F4F6", (string)fonts[0].Attribute("Foreground")!);
    }

    [Fact]
    public void Chosen_colours_replace_the_theme_defaults()
    {
        var fonts = Build(background: "#202040", text: "#EEEEAA").Elements(Ns + "Font").ToList();

        Assert.Equal("202040", (string)fonts[0].Attribute("Background")!);
        Assert.Equal("EEEEAA", (string)fonts[0].Attribute("Foreground")!);
    }

    [Fact]
    public void Light_theme_uses_a_white_window()
    {
        Assert.Equal("FFFFFF", (string)Build(light: true).Elements(Ns + "Font").First().Attribute("Background")!);
    }

    [Fact]
    public void The_controls_the_application_looks_up_by_name_survive()
    {
        var theme = Build();

        foreach (var name in new[] { "InstallButton", "InstallCancelButton", "OptionsButton", "InstallFolder", "BrowseButton", "EulaRichedit",
                                     "EulaAcceptCheckbox", "OverallCalculatedProgressbar", "RepairButton", "UninstallButton", "SuccessCloseButton",
                                     "FailureLogFileLink", "LaunchButton" })
            Assert.NotNull(Named(theme, name));
    }

    [Fact]
    public void License_style_decides_which_license_controls_remain()
    {
        Assert.Equal("Hypertext", Named(Build(license: BundleTheme.LicenseStyle.Link), "EulaHyperlink"));
        Assert.Null(Named(Build(license: BundleTheme.LicenseStyle.Link), "EulaRichedit"));
        Assert.Null(Named(Build(license: BundleTheme.LicenseStyle.None), "EulaRichedit"));
        Assert.Null(Named(Build(license: BundleTheme.LicenseStyle.None), "EulaAcceptCheckbox"));
    }

    [Fact]
    public void Palette_derives_readable_secondary_colours()
    {
        var dark = Palette.Create(false, "#0067C0", null, null);

        Assert.True(dark.IsDark);
        Assert.True(dark.Heading.Luminance > dark.Accent.Luminance, "the accent is lightened to be read on the dark background");
        Assert.Equal(Palette.DefaultAccent.Hex, Palette.Create(false, "nonsense", null, null).Accent.Hex);
    }
}
