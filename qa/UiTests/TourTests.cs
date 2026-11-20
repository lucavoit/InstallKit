using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// Goes through every page of the setup that a person sees and takes a screenshot of each, so the look of the dialogs can be
/// reviewed from a CI run without a Windows machine: the artifact of the run holds the pictures (<c>NN-name.png</c> of the screen
/// and <c>NN-name.windowN.png</c> of each window) next to the UI trees. The tests also check that the pages are the ones expected
/// and that the flows lead through them, so a page that is restyled but no longer reachable fails here.
/// </summary>
public class TourTests
{
    // Windows Installer's ERROR_INSTALL_USEREXIT.
    const int UserExit = 1602;

    readonly ITestOutputHelper output;

    public TourTests(ITestOutputHelper output) => this.output = output;

    [UiFact]
    public void Tour_of_the_install_pages_from_welcome_to_finish()
    {
        using var product = new InstalledProduct(output);
        using var setup = SetupSession.Start(output, "tour-install");

        setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Setup will install"));
        setup.Show("install-1-welcome");

        setup.Click("Next");
        setup.WaitForPage("settings", p => p.HasText("Region") && p.HasText("Install to"));
        setup.Show("install-2-settings");

        setup.Click("Next");
        setup.WaitForPage("features", p => p.HasText("Custom Setup"));
        setup.Show("install-3-features");

        setup.Click("Next");
        setup.WaitForPage("ready to install", p => p.HasButton("Install") && p.HasText("Ready to install"));
        setup.Show("install-4-ready");

        // The cancel prompt on top of the page, then back to the page.
        setup.Click("Cancel");
        setup.WaitForPage("cancel question", p => p.HasButton("Yes") && p.HasButton("No"));
        setup.Show("install-5-cancel-prompt");
        setup.Click("No");
        setup.WaitForPage("ready to install again", p => p.HasButton("Install") && p.HasText("Ready to install"));

        setup.Click("Install");
        setup.TryShow("install-6-progress", p => p.HasText("Installing"));

        setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), TimeSpan.FromMinutes(4));
        setup.Show("install-7-finish");
        setup.Click("Finish");
        Assert.Equal(0, setup.WaitForExit());
        Assert.True(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "The tour installed the product.");
    }

    [UiFact]
    public void Tour_of_the_interrupted_page()
    {
        using var product = new InstalledProduct(output);
        using var setup = SetupSession.Start(output, "tour-interrupted");

        setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Setup will install"));
        setup.Click("Cancel");
        setup.WaitForPage("cancel question", p => p.HasButton("Yes"));
        setup.Click("Yes");
        setup.WaitForPage("interrupted", p => p.HasText("interrupted") && p.HasButton("Finish"));
        setup.Show("interrupted");
        setup.Click("Finish");
        Assert.Equal(UserExit, setup.WaitForExit());
    }

    [UiFact]
    public void Tour_of_the_repair_and_remove_pages()
    {
        using var product = new InstalledProduct(output);
        product.InstallSilently();

        // Repair: maintenance welcome, the choice, "ready to repair", progress, finish.
        using (var setup = SetupSession.Start(output, "tour-repair"))
        {
            setup.WaitForPage("maintenance welcome", p => p.HasButton("Next") && !p.HasText("Install to"));
            setup.Show("repair-1-maintenance-welcome");

            setup.Click("Next");
            setup.WaitForPage("maintenance type", p => p.HasButton("Change") && p.HasButton("Repair") && p.HasButton("Remove"));
            setup.Show("repair-2-maintenance-type");

            setup.Click("Repair");
            setup.WaitForPage("ready to repair", p => p.HasText("Ready to repair") && p.HasButton("Repair"));
            setup.Show("repair-3-ready");

            setup.Click("Repair");
            setup.TryShow("repair-4-progress", p => p.HasText("Repairing"));
            setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), TimeSpan.FromMinutes(4));
            setup.Show("repair-5-finish");
            setup.Click("Finish");
            Assert.Equal(0, setup.WaitForExit());
        }

        // Remove: the same pages ending in an uninstall.
        using (var setup = SetupSession.Start(output, "tour-remove"))
        {
            setup.WaitForPage("maintenance welcome", p => p.HasButton("Next") && !p.HasText("Install to"));
            setup.Click("Next");
            setup.WaitForPage("maintenance type", p => p.HasButton("Remove"));
            setup.Click("Remove");
            setup.WaitForPage("ready to remove", p => p.HasText("Ready to remove") && p.HasButton("Remove"));
            setup.Show("remove-1-ready");

            setup.Click("Remove");
            setup.TryShow("remove-2-progress", p => p.HasText("Removing"));
            setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), TimeSpan.FromMinutes(4));
            setup.Show("remove-3-finish");
            setup.Click("Finish");
            Assert.Equal(0, setup.WaitForExit());
        }

        Assert.False(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "Remove took the product off the machine.");
    }

    [UiBundleFact]
    public void Tour_of_the_bundle_window()
    {
        using var setup = SetupSession.StartBundle(output, "tour-bundle");

        try
        {
            setup.WaitForPage("bundle install page", p => p.HasButton("Install"), TimeSpan.FromSeconds(45));
        }
        catch (Xunit.Sdk.XunitException)
        {
            output.WriteLine(setup.Diagnostics("tour-bundle"));
            throw;
        }

        setup.Show("bundle-1-install");

        setup.Click("Options");
        setup.WaitForPage("bundle options page", p => p.HasButton("OK"));
        setup.Show("bundle-2-options");

        setup.Click("Cancel");
        setup.WaitForPage("bundle install page again", p => p.HasButton("Install"));
        // Closing the window asks first, as the MSI pages do.
        setup.Click("Close");
        setup.WaitForPage("bundle cancel question", p => p.HasButton("Yes"));
        setup.Show("bundle-3-cancel-prompt");
        setup.Click("Yes");
        setup.WaitForExit(TimeSpan.FromSeconds(60));
    }
}
