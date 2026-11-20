using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using InstallKit.WixSharp;
using Xunit;

namespace InstallKit.WixSharp.Tests;

public class ModernDialogTests
{
    static readonly XNamespace Ns = "http://wixtoolset.org/schemas/v4/wxs";

    static readonly ModernPrompt[] TwoPrompts = { new("SERVER", "Server address", required: true), new("PORT", "Port", required: false) };

    static XElement Build(bool features = false, bool chooseDir = false, string? license = null, bool prompts = false) =>
        ModernDialogXml.BuildFragment(Ns, new ModernDialogOptions
        {
            HasFeatures = features,
            ChooseInstallDir = chooseDir,
            LicenseFile = license,
            Prompts = prompts ? TwoPrompts : new ModernPrompt[0],
        });

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
        from prompts in new[] { false, true }
        select new object[] { features, chooseDir, license!, prompts };

    [Theory]
    [MemberData(nameof(Variants))]
    public void Every_dialog_we_navigate_to_exists_or_is_referenced(bool features, bool chooseDir, string? license, bool prompts)
    {
        var fragment = Build(features, chooseDir, license, prompts);
        var known = new HashSet<string>(DialogRefs(fragment).Concat(Dialogs(fragment).Select(d => (string)d.Attribute("Id")!)));

        var targets = Publishes(fragment).Where(p => p.Event is "NewDialog" or "SpawnDialog").Select(p => p.Value)
            .Concat(Ui(fragment).Elements(Ns + "InstallUISequence").Elements().Select(e => (string)e.Attribute("Dialog")!)).Distinct().ToList();

        Assert.All(targets, target => Assert.Contains(target, known));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Control_events_are_unique_and_controls_have_unique_ids(bool features, bool chooseDir, string? license, bool prompts)
    {
        var fragment = Build(features, chooseDir, license, prompts);

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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void The_ok_button_of_the_folder_browser_has_events_whenever_the_browser_is_referenced(bool features, bool chooseDir)
    {
        var fragment = Build(features, chooseDir);
        var ok = Publishes(fragment).Where(p => p.Dialog == "BrowseDlg" && p.Control == "OK").OrderBy(p => p.Order).ToList();

        if (!DialogRefs(fragment).Contains("BrowseDlg"))
        {
            Assert.Empty(ok);
            return;
        }

        // Without events the button does nothing (ICE17), and the chosen folder is never applied.
        Assert.Equal(new[] { ("SetTargetPath", "[_BrowseProperty]"), ("EndDialog", "Return") }, ok.Select(p => (p.Event, p.Value)));
        Assert.Equal(new int?[] { 3, 4 }, ok.Select(p => p.Order));
    }

    [Fact]
    public void The_maintenance_dialogs_come_with_their_own_schedule_from_the_wix_ui_extension()
    {
        var fragment = Build();
        var shows = Ui(fragment).Single(e => e.Name == Ns + "InstallUISequence").Elements(Ns + "Show")
            .ToDictionary(e => (string)e.Attribute("Dialog")!, e => (string?)e.Attribute("Condition"));

        Assert.Equal("NOT Installed", shows[ModernDialogXml.WelcomeId]);
        Assert.Contains(ModernDialogXml.FinishId, shows.Keys);

        // Running the MSI again on an installed product shows these. The extension schedules them itself (as virtual
        // entries), so scheduling them here as well fails the build with WIX7009.
        Assert.Contains("ResumeDlg", DialogRefs(fragment));
        Assert.Contains("MaintenanceWelcomeDlg", DialogRefs(fragment));
        Assert.DoesNotContain("ResumeDlg", shows.Keys);
        Assert.DoesNotContain("MaintenanceWelcomeDlg", shows.Keys);
    }

    [Fact]
    public void The_license_and_its_accept_box_sit_on_the_welcome_page_with_the_box_above_the_text()
    {
        var fragment = Build(features: true, chooseDir: true, license: "license.rtf");
        var welcome = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var controls = welcome.Elements(Ns + "Control").ToDictionary(c => (string)c.Attribute("Id")!);

        Assert.Contains("License", controls.Keys);
        Assert.Contains("Intro", controls.Keys);
        Assert.True((int)controls["Accept"].Attribute("Y")! < (int)controls["License"].Attribute("Y")!);
        Assert.True((int)controls["Intro"].Attribute("Y")! < (int)controls["Accept"].Attribute("Y")!);
        Assert.DoesNotContain("Folder", controls.Keys);

        var install = controls["Install"];
        Assert.Equal("Next", (string)install.Attribute("Text")!);
        Assert.Contains("InstallKitLicenseAccepted", (string)install.Attribute("DisableCondition")!);
        Assert.Contains("InstallKitLicenseAccepted", (string)install.Attribute("EnableCondition")!);
    }

    [Fact]
    public void Without_a_license_the_next_button_is_not_gated()
    {
        var welcome = Dialogs(Build(chooseDir: true)).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var install = welcome.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Install");

        Assert.Null(install.Attribute("DisableCondition"));
        Assert.DoesNotContain(welcome.Elements(Ns + "Control"), c => (string)c.Attribute("Id")! == "License");
    }

    [Fact]
    public void The_install_folder_and_the_prompts_share_the_page_after_the_welcome_page()
    {
        var fragment = Build(chooseDir: true, license: "license.rtf", prompts: true);
        var publishes = Publishes(fragment).ToList();

        var options = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
        var controls = options.Elements(Ns + "Control").Select(c => (string)c.Attribute("Id")!).ToList();
        Assert.Contains("Folder", controls);
        Assert.Contains("Browse", controls);
        Assert.Contains("Prompt1", controls);
        Assert.Contains("Prompt2", controls);

        // The folder is applied before the page is left, and the page leads on from the welcome page.
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Next" && p.Event == "SetTargetPath" && p.Value == "INSTALLDIR");
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install" && p.Event == "NewDialog" && p.Value == ModernDialogXml.OptionsId);
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Back" && p.Value == ModernDialogXml.WelcomeId);
    }

    [Fact]
    public void The_folder_page_with_features_leads_to_the_feature_page_and_back_returns_to_it()
    {
        var publishes = Publishes(Build(features: true, chooseDir: true)).ToList();

        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Next" && p.Event == "NewDialog" && p.Value == "CustomizeDlg");
        Assert.Contains(publishes, p => p.Dialog == "CustomizeDlg" && p.Control == "Back" && p.Value == ModernDialogXml.OptionsId && p.Condition == "WixUI_InstallMode <> \"Change\"");
    }

    [Fact]
    public void Without_a_folder_or_prompts_the_license_welcome_page_starts_the_install()
    {
        var fragment = Build(license: "license.rtf");

        Assert.DoesNotContain(Dialogs(fragment), d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
        Assert.Contains(Publishes(fragment), p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install" && p.Event == "EndDialog" && p.Value == "Return");
    }

    [Fact]
    public void Prompts_get_a_settings_page_between_the_welcome_page_and_the_install()
    {
        var fragment = Build(prompts: true);

        var welcome = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var welcomeButton = welcome.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Install");
        Assert.Equal("Next", (string)welcomeButton.Attribute("Text")!);

        var options = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
        var edits = options.Elements(Ns + "Control").Where(c => (string)c.Attribute("Type")! == "Edit").ToList();
        Assert.Equal(new[] { "SERVER", "PORT" }, edits.Select(c => (string)c.Attribute("Property")!));
        Assert.Contains("{\\InstallKit_Font_Body}Server address", options.Elements(Ns + "Control").Select(c => (string?)c.Attribute("Text")));

        var publishes = Publishes(fragment).ToList();
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.WelcomeId && p.Control == "Install" && p.Event == "NewDialog" && p.Value == ModernDialogXml.OptionsId);
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Back" && p.Value == ModernDialogXml.WelcomeId);

        // Without features the settings page is the last page: it checks the disk space and starts the install.
        var next = options.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Next");
        Assert.Equal("Install", (string)next.Attribute("Text")!);
        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Next" && p.Event == "EndDialog" && p.Value == "Return");
    }

    [Fact]
    public void A_required_prompt_disables_next_while_its_box_is_empty()
    {
        var options = Dialogs(Build(prompts: true)).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
        var next = options.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Next");

        Assert.Equal("SERVER = \"\"", (string)next.Attribute("DisableCondition")!);

        // A condition is never undone by Windows Installer: the button needs its own enabling condition, or it stays
        // disabled after the user empties the box and types a value again.
        Assert.Equal("SERVER <> \"\"", (string)next.Attribute("EnableCondition")!);
    }

    [Fact]
    public void Several_required_prompts_all_have_to_be_filled_to_enable_next()
    {
        var fragment = ModernDialogXml.BuildFragment(Ns, new ModernDialogOptions
        {
            Prompts = new[] { new ModernPrompt("A", "A", required: true), new ModernPrompt("B", "B", required: true), new ModernPrompt("C", "C", required: false) },
        });
        var options = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
        var next = options.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Next");

        Assert.Equal("A = \"\" OR B = \"\"", (string)next.Attribute("DisableCondition")!);
        Assert.Equal("A <> \"\" AND B <> \"\"", (string)next.Attribute("EnableCondition")!);
    }

    [Fact]
    public void With_features_and_prompts_the_settings_page_leads_to_the_feature_page_and_back_goes_to_it()
    {
        var fragment = Build(features: true, prompts: true);
        var publishes = Publishes(fragment).ToList();

        Assert.Contains(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Next" && p.Event == "NewDialog" && p.Value == "CustomizeDlg");
        Assert.DoesNotContain(publishes, p => p.Dialog == ModernDialogXml.OptionsId && p.Control == "Next" && p.Event == "EndDialog");
        var back = publishes.Single(p => p.Dialog == "CustomizeDlg" && p.Control == "Back" && p.Order == 2);
        Assert.Equal(ModernDialogXml.OptionsId, back.Value);
        Assert.Equal("WixUI_InstallMode <> \"Change\"", back.Condition);
    }

    [Fact]
    public void Without_prompts_there_is_no_settings_page()
    {
        var fragment = Build();

        Assert.DoesNotContain(Dialogs(fragment), d => (string)d.Attribute("Id")! == ModernDialogXml.OptionsId);
    }

    [Fact]
    public void Page_background_covers_the_whole_dialog_and_drops_the_footer_line()
    {
        var fragment = ModernDialogXml.BuildFragment(Ns, new ModernDialogOptions { PageBackground = @"C:\obj\app.page.bmp" });

        var binary = fragment.Element(Ns + "Binary")!;
        Assert.Equal(ModernDialogXml.PageBitmapId, (string)binary.Attribute("Id")!);

        var welcome = Dialogs(fragment).Single(d => (string)d.Attribute("Id")! == ModernDialogXml.WelcomeId);
        var background = welcome.Elements(Ns + "Control").Single(c => (string)c.Attribute("Id")! == "Background");
        Assert.Equal("270", (string)background.Attribute("Height")!);
        Assert.Equal(ModernDialogXml.PageBitmapId, (string)background.Attribute("Text")!);
        Assert.DoesNotContain(welcome.Elements(Ns + "Control"), c => (string)c.Attribute("Id")! == "BottomLine");
    }

    static XElement BuildSkinned(bool features, bool chooseDir, bool prompts, bool cancel = true) =>
        ModernDialogXml.BuildFragment(Ns, new ModernDialogOptions
        {
            HasFeatures = features,
            ChooseInstallDir = chooseDir,
            Prompts = prompts ? TwoPrompts : new ModernPrompt[0],
            PageBackground = @"C:\obj\app.page.bmp",
            CancelBackground = cancel ? @"C:\obj\app.cancel.bmp" : null,
        });

    public static IEnumerable<object[]> SkinnedVariants() =>
        from features in new[] { false, true }
        from chooseDir in new[] { false, true }
        from prompts in new[] { false, true }
        select new object[] { features, chooseDir, prompts };

    [Theory]
    [MemberData(nameof(SkinnedVariants))]
    public void Restyled_stock_dialogs_replace_WiXs_and_every_target_still_exists(bool features, bool chooseDir, bool prompts)
    {
        var fragment = BuildSkinned(features, chooseDir, prompts);
        var ids = Dialogs(fragment).Select(d => (string)d.Attribute("Id")!).ToList();
        var refs = DialogRefs(fragment).ToList();

        // Ours replace WiX's: a dialog both defined here and referenced from the extension would be defined twice.
        Assert.Empty(ids.Intersect(refs));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains("InstallKit_CancelDlg", ids);
        Assert.Contains("InstallKit_VerifyReadyDlg", ids);

        var known = new HashSet<string>(refs.Concat(ids));
        var targets = Publishes(fragment).Where(p => p.Event is "NewDialog" or "SpawnDialog").Select(p => p.Value)
            .Concat(Ui(fragment).Elements(Ns + "InstallUISequence").Elements().Select(e => (string)e.Attribute("Dialog")!)).Distinct();
        Assert.All(targets, target => Assert.Contains(target, known));

        foreach (var dialog in Dialogs(fragment))
        {
            var controls = dialog.Elements(Ns + "Control").Select(c => (string)c.Attribute("Id")!).ToList();
            Assert.True(controls.Count == controls.Distinct().Count(), $"{(string)dialog.Attribute("Id")!} has duplicate control ids");
        }

        var keys = Publishes(fragment).Select(p => (p.Dialog, p.Control, p.Event, p.Value, p.Condition)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Restyled_stock_dialogs_are_scheduled_by_us_not_by_the_extension()
    {
        var fragment = BuildSkinned(features: false, chooseDir: false, prompts: false);
        var install = Ui(fragment).Single(e => e.Name == Ns + "InstallUISequence").Elements(Ns + "Show").ToList();
        var admin = Ui(fragment).Single(e => e.Name == Ns + "AdminUISequence").Elements(Ns + "Show").Select(e => (string)e.Attribute("Dialog")!).ToList();

        Assert.Equal("ExecuteAction", (string)install.Single(e => (string)e.Attribute("Dialog")! == "InstallKit_ProgressDlg").Attribute("Before")!);
        Assert.Equal("InstallKit_ProgressDlg", (string)install.Single(e => (string)e.Attribute("Dialog")! == "InstallKit_MaintenanceWelcomeDlg").Attribute("Before")!);
        Assert.Equal("cancel", (string)install.Single(e => (string)e.Attribute("Dialog")! == "InstallKit_UserExit").Attribute("OnExit")!);
        Assert.Contains("InstallKit_FatalError", admin);

        var dialogs = install.Select(e => (string)e.Attribute("Dialog")!).ToList();
        Assert.Equal(dialogs.Count, dialogs.Distinct().Count());
    }

    [Fact]
    public void Without_a_cancel_background_the_cancel_prompt_stays_WiXs()
    {
        var fragment = BuildSkinned(features: false, chooseDir: false, prompts: false, cancel: false);

        Assert.Contains("CancelDlg", DialogRefs(fragment));
        Assert.DoesNotContain(Dialogs(fragment), d => (string)d.Attribute("Id")! == "InstallKit_CancelDlg");
    }

    [Fact]
    public void The_error_popup_and_the_folder_and_feature_pages_keep_WiXs_look()
    {
        var refs = DialogRefs(BuildSkinned(features: true, chooseDir: true, prompts: false)).ToList();

        // Windows Installer replaces the error text (and so drops our text style), so that popup cannot be dark.
        Assert.Contains("ErrorDlg", refs);
        Assert.Contains("BrowseDlg", refs);
        Assert.Contains("CustomizeDlg", refs);
    }
}
