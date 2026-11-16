using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// The parts of the modern setup that change something: a value typed into a page ends up in an installed file, the folder
/// browser applies its choice, and running the installed product again offers repair, remove and change.
/// </summary>
public class InstallFlowTests
{
    // Windows Installer's ERROR_INSTALL_USEREXIT.
    const int UserExit = 1602;

    readonly ITestOutputHelper output;

    public InstallFlowTests(ITestOutputHelper output) => this.output = output;

    [UiFact]
    public void A_required_value_must_be_filled_in_and_reaches_the_installed_config()
    {
        using var product = new InstalledProduct(output);
        using var setup = SetupSession.Start(output, "install-with-typed-value");

        setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Install to"));
        setup.Click("Next");

        // The prompt is required (QA_REGION) and starts with its default; emptying it must stop Next.
        setup.WaitForPage("settings", p => p.HasText("Region"));
        var region = setup.Edit();
        Assert.Equal("eu-west", SetupSession.ReadText(region));

        setup.SetText(region, "");
        setup.WaitUntil("Next is disabled while the required value is empty", () => !setup.IsEnabled("Next"));

        setup.SetText(region, "us-east");
        setup.WaitUntil("Next is enabled again once there is a value", () => setup.IsEnabled("Next"));
        setup.Click("Next");

        // Features (the defaults: Docs on, Samples and Worker off), then "ready to install".
        setup.WaitForPage("features", p => p.HasText("Custom Setup"));
        setup.Click("Next");
        setup.WaitForPage("ready to install", p => p.HasButton("Install"));
        setup.Click("Install");

        setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), TimeSpan.FromMinutes(4));
        setup.Click("Finish");
        Assert.Equal(0, setup.WaitForExit());

        // The typed value is in the installed config, next to the files of the default features only.
        var config = XDocument.Load(Path.Combine(InstalledProduct.InstallDir, "config", "qa.config"));
        var settings = config.Descendants("add").ToDictionary(e => (string)e.Attribute("key")!, e => (string)e.Attribute("value")!);
        Assert.Equal("us-east", settings["Region"]);
        Assert.True(File.Exists(Path.Combine(InstalledProduct.InstallDir, "docs", "readme.txt")), "The Docs feature is on by default.");
        Assert.False(Directory.Exists(Path.Combine(InstalledProduct.InstallDir, "samples")), "The Samples feature is off by default.");

        product.Uninstall();
        Assert.False(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "The product is gone after uninstalling.");
    }

    [UiFact]
    public void The_folder_browser_applies_the_chosen_folder_to_the_welcome_page()
    {
        using var setup = SetupSession.Start(output, "browse-for-the-folder");
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "installkit-ui-browse")).FullName;

        setup.WaitForPage("welcome", p => p.HasButton("Browse...") && p.HasText("Install to"));
        setup.Click("Browse...");

        setup.WaitForPage("folder browser", p => p.HasButton("OK"));
        setup.SetText(setup.EditOnWindowWith("OK"), folder);
        setup.Click("OK");

        // OK must close the browser and hand the folder back. A button without events (ICE17) leaves the dialog open.
        setup.WaitUntil("the folder browser closes", () => !setup.IsEnabled("OK"));
        setup.WaitUntil("the welcome page shows the chosen folder",
            () => Same(SetupSession.ReadText(setup.Edit()), folder));

        Cancel(setup);
    }

    [UiFact]
    public void Running_the_installed_product_again_offers_repair_remove_and_change()
    {
        using var product = new InstalledProduct(output);
        product.InstallSilently();

        using var setup = SetupSession.Start(output, "maintenance");

        // An installed product greets with the maintenance pages instead of the install pages.
        setup.WaitForPage("maintenance welcome", p => p.HasButton("Next") && !p.HasText("Install to"));
        setup.Click("Next");

        setup.WaitForPage("maintenance type", p => p.HasButton("Change") && p.HasButton("Repair") && p.HasButton("Remove"));

        // Change opens the same feature page as the first install.
        setup.Click("Change");
        setup.WaitForPage("features", p => p.HasText("Custom Setup") && p.HasText("Samples"));

        Cancel(setup);
    }

    // Cancel asks first; Yes leads to the "interrupted" page, and Finish ends the setup with "user exit".
    static void Cancel(SetupSession setup)
    {
        setup.Click("Cancel");
        setup.WaitForPage("cancel question", p => p.HasButton("Yes"));
        setup.Click("Yes");
        setup.WaitForPage("interrupted", p => p.HasText("interrupted") && p.HasButton("Finish"));
        setup.Click("Finish");
        Assert.Equal(UserExit, setup.WaitForExit());
    }

    // Folder paths differ in case and in a trailing backslash after Windows Installer has normalised them.
    static bool Same(string shown, string expected) =>
        string.Equals(shown.TrimEnd('\\'), expected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
