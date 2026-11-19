using System;
using Xunit;

namespace InstallKit.UiTests;

/// <summary>A test that needs a Windows desktop and an MSI to start. Skipped, with the reason, when either is missing.</summary>
public sealed class UiFactAttribute : FactAttribute
{
    public UiFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "UI tests drive the Windows Installer dialogs and only run on Windows.";
        else if (string.IsNullOrWhiteSpace(SetupSession.MsiPath))
            Skip = $"Set {SetupSession.MsiVariable} to the MSI to start.";
        else if (SetupSession.IsFlat)
            Skip = "This MSI has the flat WPF dialogs; the native dialog tests do not apply.";
    }
}

/// <summary>A test of the flat WPF dialogs: only runs when <see cref="SetupSession.FlatVariable"/> is 1 (the MSI was built with them).</summary>
public sealed class FlatUiFactAttribute : FactAttribute
{
    public FlatUiFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "UI tests drive the Windows Installer dialogs and only run on Windows.";
        else if (string.IsNullOrWhiteSpace(SetupSession.MsiPath))
            Skip = $"Set {SetupSession.MsiVariable} to the MSI to start.";
        else if (!SetupSession.IsFlat)
            Skip = $"Set {SetupSession.FlatVariable}=1 when the MSI has the flat WPF dialogs.";
    }
}

/// <summary>A test that needs a Windows desktop and the setup .exe of a bundle to start.</summary>
public sealed class UiBundleFactAttribute : FactAttribute
{
    public UiBundleFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "UI tests drive Windows setup windows and only run on Windows.";
        else if (string.IsNullOrWhiteSpace(SetupSession.BundlePath))
            Skip = $"Set {SetupSession.BundleVariable} to the bundle .exe to start.";
    }
}
