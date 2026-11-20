using Xunit;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// Walks the modern dialogs of the QA demo (an install folder, a prompted value and two optional features) and cancels, so
/// nothing is installed. It checks what the build-time checks cannot: that the dialogs we generate are the ones that show up
/// and that their buttons lead where they should.
/// </summary>
public class WizardTests
{
    // Windows Installer's ERROR_INSTALL_USEREXIT.
    const int UserExit = 1602;

    readonly ITestOutputHelper output;

    public WizardTests(ITestOutputHelper output) => this.output = output;

    [UiFact]
    public void Next_walks_welcome_settings_and_features_and_cancel_leaves_the_machine_alone()
    {
        using var setup = SetupSession.Start(output, "walk-and-cancel");

        // Welcome: the QA demo has features, so this page leads on ("Next") instead of installing ("Install").
        var welcome = setup.WaitForPage("welcome", p => p.HasButton("Next") && p.HasText("Setup will install") && p.HasText("accept"));
        Assert.False(welcome.HasButton("Install"), "With optional features the welcome page must not start the install.");
        setup.Dump("welcome");

        // Next waits for the license (the accept box is above its text).
        Assert.False(setup.IsEnabled("Next"), "Next must wait for the license to be accepted.");
        setup.Check("accept");

        // Settings: the install folder and the prompted value QA_REGION.
        setup.Click("Next");
        setup.WaitForPage("settings", p => p.HasText("Region") && p.HasText("Install to"));
        setup.Dump("settings");

        // The feature page lists the two optional features.
        setup.Click("Next");
        setup.WaitForPage("features", p => p.HasText("Samples"));
        setup.Dump("features");

        // Cancel asks first. Yes leads to the "interrupted" page, which waits for Finish; the setup then ends with
        // "user exit" and has installed nothing.
        setup.Click("Cancel");
        setup.WaitForPage("cancel question", p => p.HasButton("Yes"));
        setup.Click("Yes");
        setup.WaitForPage("interrupted", p => p.HasText("interrupted") && p.HasButton("Finish"));
        setup.Click("Finish");
        Assert.Equal(UserExit, setup.WaitForExit());
    }
}
