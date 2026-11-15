using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using InstallKit.WixSharp;
using Xunit;

namespace InstallKit.WixSharp.Tests;

public class ModernDialogTests
{
    static readonly XNamespace Ns = "http://wixtoolset.org/schemas/v4/wxs";

    static XElement Build(bool features = false, bool chooseDir = false, string? license = null) =>
        ModernDialogXml.BuildFragment(Ns, new ModernDialogOptions { HasFeatures = features, ChooseInstallDir = chooseDir, LicenseFile = license });

    static IEnumerable<XElement> Ui(XElement fragment) => fragment.Element(Ns + "UI")!.Elements();

    static IEnumerable<string> DialogRefs(XElement fragment) => Ui(fragment).Where(e => e.Name == Ns + "DialogRef").Select(e => (string)e.Attribute("Id")!);

    static IEnumerable<XElement> Dialogs(XElement fragment) => Ui(fragment).Where(e => e.Name == Ns + "Dialog");

    // Every Publish of the fragment, including the ones inside our own dialogs' controls, with where it lives.
    static IEnumerable<(string Dialog, string Control, string Event, string Value, int? Order, string? Condition)> Publishes(XElement fragment)
    {
        foreach (var publish in Ui(fragment).Where(e => e.Name == Ns + "Publish"))
            yield return ((string)publish.Attribute("Dialog")!, (string)publish.Attribute("Control")!, (string)publish.Attribute("Event")!,
                (string)publish.Attribute("Value")!, (int?)publish.Attribute("Order"), (string?)publish.Attribute("Condition"));

        foreach (var dialog in Dialogs(fragment))
        foreach (var control in dialog.Elements(Ns + "Control"))
        foreach (var publish in control.Elements(Ns + "Publish").Where(p => p.Attribute("Event") != null))
            yield return ((string)dialog.Attribute("Id")!, (string)control.Attribute("Id")!, (string)publish.Attribute("Event")!,
                (string)publish.Attribute("Value")!, (int?)publish.Attribute("Order"), (string?)publish.Attribute("Condition"));
    }

    static string? Property(XElement fragment, string id) =>
        Ui(fragment).Where(e => e.Name == Ns + "Property" && (string?)e.Attribute("Id") == id).Select(e => (string?)e.Attribute("Value")).SingleOrDefault();

    public static IEnumerable<object[]> Variants() =>
        from features in new[] { false, true }
        from chooseDir in new[] { false, true }
        from license in new string?[] { null, "license.rtf" }
        select new object[] { features, chooseDir, license! };

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_dialog_we_navigate_to_exists_or_is_referenced(bool features, bool chooseDir, string? license)
    {
        var fragment = Build(features, chooseDir, license);
        var known = new HashSet<string>(DialogRefs(fragment).Concat(Dialogs(fragment).Select(d => (string)d.Attribute("Id")!)));

        var targets = Publishes(fragment).Where(p => p.Event is "NewDialog" or "SpawnDialog").Select(p => p.Value)
            .Concat(Ui(fragment).Elements(Ns + "InstallUISequence").Elements().Select(e => (string)e.Attribute("Dialog")!)).Distinct().ToList();

        Assert.All(targets, target => Assert.Contains(target, known));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Control_events_are_unique_and_controls_have_unique_ids(bool features, bool chooseDir, string? license)
    {
        var fragment = Build(features, chooseDir, license);

        // The ControlEvent table's key: dialog, control, event, argument and condition.
        var keys = Publishes(fragment).Select(p => (p.Dialog, p.Control, p.Event, p.Value, p.Condition)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());

        // Several NewDialog events on one control need distinct orders, or which one wins is undefined.
        foreach (var group in Publishes(fragment).Where(p => p.Event == "NewDialog").GroupBy(p => (p.Dialog, p.Control)).Where(g => g.Count() > 1))
            Assert.Equal(group.Count(), group.Select(p => p.Order).Distinct().Count());

        foreach (var dialog in Dialogs(fragment))
        {
            var ids = dialog.Elements(Ns + "Control").Select(c => (string)c.Attribute("Id")!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public void Without_features_the_install_button_starts_the_install_and_repair_has_no_change_button()
    {
        var fragment = Build();

        Assert.DoesNotContain("CustomizeDlg", DialogRefs(fragment));
        Assert.Equal("Minimal", Property(fragment, "WixUI_Mode"));
        Assert.Equal("1", Property(fragment, "ARPNOMODIFY"));

        var install = Publishes(fragment).Where(p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install").ToList();
        Assert.Contains(install, p => p.Event == "EndDialog" && p.Value == "Return");
        Assert.DoesNotContain(install, p => p.Event == "NewDialog");
        Assert.DoesNotContain(Publishes(fragment), p => p.Control == "ChangeButton");

        var back = Publishes(fragment).Single(p => p.Dialog == "VerifyReadyDlg" && p.Control == "Back");
        Assert.Equal(("MaintenanceTypeDlg", null), (back.Value, back.Condition));
    }

    [Fact]
    public void With_features_the_welcome_page_leads_to_the_feature_page_and_repair_gets_a_change_button()
    {
        var fragment = Build(features: true);

        Assert.Contains("CustomizeDlg", DialogRefs(fragment));
        Assert.Contains("DiskCostDlg", DialogRefs(fragment));
        Assert.Contains("BrowseDlg", DialogRefs(fragment));
        Assert.Equal("FeatureTree", Property(fragment, "WixUI_Mode"));
        Assert.Null(Property(fragment, "ARPNOMODIFY"));

        var welcome = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var install = welcome.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Install");
        Assert.Equal("Next", (string)install.Attribute("Text")!);
        Assert.Null(install.Attribute("ElevationShield"));

        var publishes = Publishes(fragment).ToList();
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install" && p.Event == "NewDialog" && p.Value == "CustomizeDlg");
        Assert.DoesNotContain(publishes, p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install" && p.Event == "EndDialog");
        Assert.Contains(publishes, p => p.Dialog == "MaintenanceTypeDlg" && p.Control == "ChangeButton" && p.Value == "CustomizeDlg");
        Assert.Contains(publishes, p => p.Dialog == "CustomizeDlg" && p.Control == "Next" && p.Value == "VerifyReadyDlg");
    }

    [Fact]
    public void The_back_buttons_of_the_feature_flow_go_to_the_right_place_with_exclusive_conditions()
    {
        var publishes = Publishes(Build(features: true)).ToList();

        var customizeBack = publishes.Where(p => p.Dialog == "CustomizeDlg" && p.Control == "Back").OrderBy(p => p.Order).ToList();
        Assert.Equal(new[] { "MaintenanceTypeDlg", ModernDialogXml.WelcomeId }, customizeBack.Select(p => p.Value));
        Assert.Equal("WixUI_InstallMode = \"Change\"", customizeBack[0].Condition);
        Assert.Equal("WixUI_InstallMode <> \"Change\"", customizeBack[1].Condition);

        var readyBack = publishes.Where(p => p.Dialog == "VerifyReadyDlg" && p.Control == "Back").OrderBy(p => p.Order).ToList();
        Assert.Equal(new[] { "CustomizeDlg", "MaintenanceTypeDlg" }, readyBack.Select(p => p.Value));
        Assert.Equal("NOT Installed OR WixUI_InstallMode = \"Change\"", readyBack[0].Condition);
        Assert.Equal("Installed AND WixUI_InstallMode <> \"Change\"", readyBack[1].Condition);
    }

    [Fact]
    public void The_maintenance_dialogs_are_shown_when_the_installed_product_is_run_again()
    {
        var fragment = Build();
        var shows = Ui(fragment).Single(e => e.Name == Ns + "InstallUISequence").Elements(Ns + "Show")
            .ToDictionary(e => (string)e.Attribute("Dialog")!, e => (string?)e.Attribute("Condition"));

        Assert.Equal("NOT Installed", shows[ModernDialogXml.WelcomeId]);
        Assert.Contains("Installed", shows["MaintenanceWelcomeDlg"]);
        Assert.Contains("NOT RESUME", shows["MaintenanceWelcomeDlg"]);
        Assert.Contains("RESUME", shows["ResumeDlg"]);
        Assert.Contains(ModernDialogXml.FinishId, shows.Keys);
    }

    [Fact]
    public void The_browse_button_and_the_folder_stay_available_with_a_license_and_features()
    {
        var fragment = Build(features: true, chooseDir: true, license: "license.rtf");
        var welcome = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var controls = welcome.Elements(Ns + "Control").Select(c => (string)c.Attribute("Id")!).ToList();

        Assert.Contains("License", controls);
        Assert.Contains("Accept", controls);
        Assert.Contains("Folder", controls);
        Assert.Contains("Browse", controls);
        var install = welcome.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Install");
        Assert.Contains("InstallKitLicenseAccepted", (string)install.Attribute("DisableCondition")!);
        Assert.Contains(Publishes(fragment), p => p.Control == "Install" && p.Event == "SetTargetPath" && p.Value == "INSTALLDIR");
    }
}
