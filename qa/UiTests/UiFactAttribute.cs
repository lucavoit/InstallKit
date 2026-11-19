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
