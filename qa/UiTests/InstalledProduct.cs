using System;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// The QA demo as it sits on the machine. Tests that install it use this to start from a clean machine and to leave one
/// behind even when an assertion fails halfway through. The runners are disposable, but the tests share one machine and
/// one product, so each of them has to put it back.
/// </summary>
public sealed class InstalledProduct : IDisposable
{
    // Windows Installer's ERROR_UNKNOWN_PRODUCT: removing something that is not installed.
    const int NotInstalled = 1605;

    readonly ITestOutputHelper output;

    /// <summary>Removes the product if it is installed, and removes it again when disposed.</summary>
    public InstalledProduct(ITestOutputHelper output)
    {
        this.output = output;
        Uninstall();
    }

    public static string MsiPath => Path.GetFullPath(SetupSession.MsiPath ?? throw new InvalidOperationException($"{SetupSession.MsiVariable} is not set."));

    /// <summary>Where the QA demo installs by default (an all-users x64 install).</summary>
    public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "InstallKit", "InstallKit QA Demo");

    /// <summary>Installs without any UI, the way the other QA tests do.</summary>
    public void InstallSilently()
    {
        var exitCode = Run($"/i \"{MsiPath}\" /qn /norestart");
        if (exitCode != 0)
            throw new InvalidOperationException($"msiexec /i exited with {exitCode}.");
    }

    public void Uninstall()
    {
        var exitCode = Run($"/x \"{MsiPath}\" /qn /norestart");
        if (exitCode != 0 && exitCode != NotInstalled)
            throw new InvalidOperationException($"msiexec /x exited with {exitCode}.");
    }

    public void Dispose()
    {
        try
        {
            Uninstall();
        }
        catch (Exception e)
        {
            // Never hide the failure of the test itself behind a failed cleanup.
            output.WriteLine($"cleanup failed: {e.Message}");
        }
    }

    int Run(string arguments)
    {
        output.WriteLine($"msiexec {arguments}");
        using var process = Process.Start(new ProcessStartInfo("msiexec.exe", arguments) { UseShellExecute = false, CreateNoWindow = true })
            ?? throw new InvalidOperationException("msiexec did not start.");
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"msiexec {arguments} did not finish within 5 minutes.");
        }

        return process.ExitCode;
    }
}
