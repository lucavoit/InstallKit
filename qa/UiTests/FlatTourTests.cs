using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// The flat WPF dialogs (Appearance.Flat) of the QA demo: the same tour as <see cref="TourTests"/> over their pages, with a
/// screenshot of each, plus the flows (accepting, typing a value, repair, remove, cancel). Runs only when CI built the MSI
/// with them (INSTALLKIT_UI_FLAT=1).
/// </summary>
public class FlatTourTests
{
    // Windows Installer's ERROR_INSTALL_USEREXIT.
    const int UserExit = 1602;

    readonly ITestOutputHelper output;

    public FlatTourTests(ITestOutputHelper output) => this.output = output;

    [FlatUiFact]
    public void Tour_of_the_flat_install_pages()
    {
        using var product = new InstalledProduct(output);
        using var setup = SetupSession.Start(output, "flat-install");

        // The QA demo has a settings page (install folder and a value) and features, so the welcome page leads on.
        var welcome = setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Setup will install") && p.HasText("accept"), System.TimeSpan.FromSeconds(90));
        setup.Show("flat-install-1-welcome");

        // The license can be read in another program or saved (not clicked here: that would start WordPad or a file dialog).
        Assert.True(welcome.HasButton("Open license"), "The welcome page offers to open the license.");
        Assert.True(welcome.HasButton("Save a copy"), "The welcome page offers to save a copy of the license.");

        // The QA demo has a license: Next waits for the accept box, which sits above the text.
        Assert.False(setup.IsEnabled("Next"), "Next must wait for the license to be accepted.");
        setup.Check("accept");
        setup.Show("flat-install-1b-license-accepted");
        setup.Click("Next");
        setup.WaitForPage("settings", p => p.HasText("Region") && p.HasText("Install to"));
        setup.Show("flat-install-2-settings");

        setup.Click("Next");
        setup.WaitForPage("features", p => p.HasText("Samples") && p.HasButton("Install"));
        setup.Show("flat-install-3-features");

        setup.Click("Install");
        setup.TryShow("flat-install-4-progress", p => p.HasText("Installing"));

        setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), System.TimeSpan.FromMinutes(4));
        setup.Show("flat-install-5-finish");
        setup.Click("Finish");
        Assert.Equal(0, setup.WaitForExit());
        Assert.True(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "The tour installed the product.");
    }

    [FlatUiFact]
    public void Cancelling_on_the_first_page_ends_the_setup_without_installing()
    {
        using var product = new InstalledProduct(output);
        using var setup = SetupSession.Start(output, "flat-cancel");

        setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Setup will install") && p.HasText("accept"), System.TimeSpan.FromSeconds(90));
        setup.Click("Cancel");
        Assert.Equal(UserExit, setup.WaitForExit());
        Assert.False(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "Cancelling installed nothing.");
    }

    [FlatUiFact]
    public void Tour_of_the_flat_repair_and_remove_pages()
    {
        using var product = new InstalledProduct(output);
        product.InstallSilently();

        using (var setup = SetupSession.Start(output, "flat-repair"))
        {
            setup.WaitForPage("maintenance", p => p.HasButton("Change") && p.HasButton("Repair") && p.HasButton("Remove"), System.TimeSpan.FromSeconds(90));
            setup.Show("flat-repair-1-maintenance");

            setup.Click("Repair");
            setup.TryShow("flat-repair-2-progress", p => p.HasText("Repairing"));
            setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), System.TimeSpan.FromMinutes(4));
            setup.Show("flat-repair-3-finish");
            setup.Click("Finish");
            Assert.Equal(0, setup.WaitForExit());
        }

        using (var setup = SetupSession.Start(output, "flat-remove"))
        {
            setup.WaitForPage("maintenance", p => p.HasButton("Remove"), System.TimeSpan.FromSeconds(90));
            setup.Click("Remove");
            setup.TryShow("flat-remove-1-progress", p => p.HasText("Removing"));
            setup.WaitForPage("finished", p => p.HasText("All set") && p.HasButton("Finish"), System.TimeSpan.FromMinutes(4));
            setup.Show("flat-remove-2-finish");
            setup.Click("Finish");
            Assert.Equal(0, setup.WaitForExit());
        }

        Assert.False(File.Exists(Path.Combine(InstalledProduct.InstallDir, "QaDemo.exe")), "Remove took the product off the machine.");
    }
}
