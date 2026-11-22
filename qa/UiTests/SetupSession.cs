using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit.Abstractions;

namespace InstallKit.UiTests;

/// <summary>
/// One run of an MSI with its dialogs: starts msiexec (no /qn), finds its windows through UI Automation and clicks buttons.
/// Whenever a page does not show up it writes what is on screen (the whole UI Automation tree, and a screenshot) next to
/// the msiexec log, so a failed run can be understood without seeing the machine.
/// </summary>
public sealed class SetupSession : IDisposable
{
    public const string MsiVariable = "INSTALLKIT_UI_MSI";
    public const string OutVariable = "INSTALLKIT_UI_OUT";

    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

    readonly ITestOutputHelper output;
    readonly UIA3Automation automation = new UIA3Automation();
    readonly Process process;
    readonly string[] processNames;
    string? logFile;
    int dumps;

    SetupSession(Process process, string outDir, ITestOutputHelper output, params string[] processNames)
    {
        this.process = process;
        this.processNames = processNames;
        OutDir = outDir;
        this.output = output;
    }

    public const string BundleVariable = "INSTALLKIT_UI_BUNDLE";
    public const string FlatVariable = "INSTALLKIT_UI_FLAT";

    /// <summary>The MSI has the flat WPF dialogs instead of the native ones (set by CI for that variant).</summary>
    public static bool IsFlat => Environment.GetEnvironmentVariable(FlatVariable) == "1";

    public static string? MsiPath => Environment.GetEnvironmentVariable(MsiVariable);

    public static string? BundlePath => Environment.GetEnvironmentVariable(BundleVariable);

    /// <summary>
    /// Starts the setup .exe of a bundle with its window. Burn runs the window from a copy of itself in a temporary
    /// folder and waits for it, so the windows are found by the name of the program, not by the process we started.
    /// </summary>
    public static SetupSession StartBundle(ITestOutputHelper output, string name)
    {
        var exe = Path.GetFullPath(BundlePath ?? throw new InvalidOperationException($"{BundleVariable} is not set."));
        var outDir = Environment.GetEnvironmentVariable(OutVariable);
        outDir = Path.GetFullPath(string.IsNullOrWhiteSpace(outDir) ? Path.Combine(Path.GetTempPath(), "installkit-ui") : outDir);
        Directory.CreateDirectory(outDir);

        var log = Path.Combine(outDir, name + ".bundle.log");
        var info = new ProcessStartInfo(exe, $"/log \"{log}\"") { UseShellExecute = false };
        var process = Process.Start(info) ?? throw new InvalidOperationException("The bundle did not start.");
        output.WriteLine($"started {exe} ({process.Id})");
        // Burn shows the window from its standard bootstrapper application host, a process named wixstdba.
        return new SetupSession(process, outDir, output, Path.GetFileNameWithoutExtension(exe), "wixstdba") { logFile = log };
    }

    public string OutDir { get; }

    public Process Process => process;

    /// <summary>Starts the MSI in its default UI level, so the dialogs are shown.</summary>
    public static SetupSession Start(ITestOutputHelper output, string name, string arguments = "")
    {
        var msi = Path.GetFullPath(MsiPath ?? throw new InvalidOperationException($"{MsiVariable} is not set."));
        var outDir = Environment.GetEnvironmentVariable(OutVariable);
        outDir = Path.GetFullPath(string.IsNullOrWhiteSpace(outDir) ? Path.Combine(Path.GetTempPath(), "installkit-ui") : outDir);
        Directory.CreateDirectory(outDir);

        var log = Path.Combine(outDir, name + ".msiexec.log");
        var info = new ProcessStartInfo("msiexec.exe", $"/i \"{msi}\" {arguments} /l*v \"{log}\"") { UseShellExecute = false };
        var process = Process.Start(info) ?? throw new InvalidOperationException("msiexec did not start.");
        output.WriteLine($"started msiexec {process.Id}: {info.Arguments}");
        // The managed (WPF) UI of an MSI runs in a second msiexec process, so windows are found by the name of the program too.
        return new SetupSession(process, outDir, output, "msiexec") { logFile = log };
    }

    /// <summary>The names of every element on every window of the setup: texts, buttons, tree items.</summary>
    public sealed record Page(IReadOnlyList<string> Names, IReadOnlyList<string> Buttons)
    {
        public bool HasText(string text) => Names.Any(n => n.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

        public bool HasButton(string text) => Buttons.Any(b => Same(b, text));
    }

    /// <summary>Waits until the windows of the setup show a page that satisfies <paramref name="isTheRightPage"/>.</summary>
    public Page WaitForPage(string what, Func<Page, bool> isTheRightPage, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        Page? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = Snapshot();
            if (last != null && isTheRightPage(last))
            {
                output.WriteLine($"page '{what}': buttons [{string.Join(", ", last.Buttons)}]");
                return last;
            }

            if (process.HasExited)
                break;
            Thread.Sleep(250);
        }

        Dump($"missing-{what}");
        output.WriteLine(Diagnostics());
        throw new Xunit.Sdk.XunitException(
            $"The page '{what}' did not show up within {(timeout ?? DefaultTimeout).TotalSeconds:0} seconds" +
            (process.HasExited ? $" (msiexec exited with {process.ExitCode})" : "") +
            $". Buttons seen: [{string.Join(", ", last?.Buttons ?? Array.Empty<string>())}]. The UI tree and a screenshot are in {OutDir}.");
    }

    /// <summary>Presses the button with this label (ignoring the mnemonic marks and arrows some labels have) on any window of the setup.</summary>
    public void Click(string button)
    {
        var element = Retry(() => Buttons().FirstOrDefault(b => Same(b.Name, button) && b.IsEnabled), TimeSpan.FromSeconds(15));
        if (element == null)
        {
            Dump($"missing-button-{button}");
            throw new Xunit.Sdk.XunitException($"No enabled button '{button}' on the setup windows. The UI tree and a screenshot are in {OutDir}.");
        }

        output.WriteLine($"click '{button}'");
        try
        {
            element.AsButton().Invoke();
        }
        catch (Exception e) when (e is not Xunit.Sdk.XunitException)
        {
            // Some controls do not offer the Invoke pattern: fall back to a real mouse click.
            output.WriteLine($"invoke failed ({e.GetType().Name}), clicking with the mouse");
            element.Click();
        }
    }

    /// <summary>Ticks the check box whose label contains <paramref name="label"/> (the license's accept box) on any window of the setup.</summary>
    public void Check(string label)
    {
        var box = Retry(() => Windows().SelectMany(w => w.FindAllDescendants())
            .FirstOrDefault(e => SafeControlType(e) == ControlType.CheckBox && SafeName(e).IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0),
            TimeSpan.FromSeconds(15));
        if (box == null)
        {
            Dump($"missing-checkbox-{label}");
            throw new Xunit.Sdk.XunitException($"No check box '{label}' on the setup windows. The UI tree and a screenshot are in {OutDir}.");
        }

        output.WriteLine($"check '{label}'");
        try
        {
            box.AsCheckBox().IsChecked = true;
        }
        catch (Exception e) when (e is not Xunit.Sdk.XunitException)
        {
            // Some check boxes do not offer the Toggle pattern: fall back to a real mouse click.
            output.WriteLine($"toggle failed ({e.GetType().Name}), clicking with the mouse");
            box.Click();
        }
    }

    /// <summary>
    /// Fills in the "File name" box of the Windows Save As dialog that is open and presses Save. The box is a combo box with an Edit
    /// inside it, which <see cref="Edits"/> leaves out, so it is found by its name.
    /// </summary>
    public void SaveFileAs(string path)
    {
        var edit = Retry(() => Windows().SelectMany(w => w.FindAllDescendants())
            .FirstOrDefault(e => SafeControlType(e) == ControlType.Edit && Same(SafeName(e), "File name")),
            TimeSpan.FromSeconds(20));
        if (edit == null)
        {
            Dump("missing-save-as-file-name");
            throw new Xunit.Sdk.XunitException($"No 'File name' box on the setup windows (is the Save As dialog open?). The UI tree and a screenshot are in {OutDir}.");
        }

        SetText(edit, path);
        Click("Save");
    }

    /// <summary>Waits until <paramref name="condition"/> holds; dumps the screen and fails with <paramref name="what"/> if it does not.</summary>
    public void WaitUntil(string what, Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                    return;
            }
            catch (Exception e) when (e is not Xunit.Sdk.XunitException)
            {
                // The page changed while we were looking: look again.
            }

            Thread.Sleep(250);
        }

        Dump($"timeout-{what}");
        throw new Xunit.Sdk.XunitException($"Timed out waiting until {what}. The UI tree and a screenshot are in {OutDir}.");
    }

    /// <summary>Whether the button with this label exists on a window of the setup and can be pressed.</summary>
    public bool IsEnabled(string button) => Buttons().Any(b => Same(b.Name, button) && b.IsEnabled);

    /// <summary>
    /// The input boxes the setup shows (the install folder, a prompted value, the folder browser's path). Windows Installer
    /// draws them as RichEdit controls; the combo box of the folder browser has a plain Edit inside it, which is left out.
    /// </summary>
    public IReadOnlyList<AutomationElement> Edits() => Windows().SelectMany(EditsOf).ToList();

    /// <summary>The input box of the window that has this button, for when several windows (a dialog over a page) are open.</summary>
    public AutomationElement EditOnWindowWith(string button)
    {
        foreach (var window in Windows())
        {
            if (window.FindAllDescendants().Any(e => SafeControlType(e) == ControlType.Button && Same(SafeName(e), button)))
            {
                var edit = EditsOf(window).FirstOrDefault();
                if (edit != null)
                    return edit;
            }
        }

        Dump($"missing-edit-near-{button}");
        throw new Xunit.Sdk.XunitException($"No input box on the window with the button '{button}'. The UI tree and a screenshot are in {OutDir}.");
    }

    /// <summary>The only input box on screen.</summary>
    public AutomationElement Edit()
    {
        var edits = Edits();
        if (edits.Count == 1)
            return edits[0];

        Dump("expected-one-edit");
        throw new Xunit.Sdk.XunitException($"Expected one input box on screen, found {edits.Count}. The UI tree and a screenshot are in {OutDir}.");
    }

    public static string ReadText(AutomationElement edit)
    {
        if (edit.Patterns.Value.IsSupported)
            return edit.Patterns.Value.Pattern.Value.ValueOrDefault ?? "";
        if (edit.Patterns.Text.IsSupported)
            return edit.Patterns.Text.Pattern.DocumentRange.GetText(-1).TrimEnd('\r', '\n');
        return edit.Name ?? "";
    }

    /// <summary>
    /// Replaces the text of an input box the way a person does: click into it, select everything, type, and leave with Tab.
    /// Setting the value through UI Automation instead does not reliably reach the Windows Installer property: it looked
    /// right on screen while a button that depends on the property stayed disabled.
    /// </summary>
    public void SetText(AutomationElement edit, string text)
    {
        output.WriteLine($"set text '{text}'");
        edit.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        if (text.Length == 0)
            Keyboard.Type(VirtualKeyShort.DELETE);
        else
            Keyboard.Type(text);

        // The box handles the keystrokes after SendInput returns, so it may still be catching up.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (ReadText(edit) != text && DateTime.UtcNow < deadline)
            Thread.Sleep(100);
        output.WriteLine($"the box now shows '{ReadText(edit)}'");
        Keyboard.Type(VirtualKeyShort.TAB);
    }

    /// <summary>Waits for msiexec to end and returns its exit code.</summary>
    public int WaitForExit(TimeSpan? timeout = null)
    {
        if (!process.WaitForExit((int)(timeout ?? TimeSpan.FromSeconds(60)).TotalMilliseconds))
        {
            Dump("msiexec-did-not-exit");
            throw new Xunit.Sdk.XunitException($"msiexec did not exit. The UI tree and a screenshot are in {OutDir}.");
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Takes the screenshot of a page that is meant to be looked at, once a page that satisfies <paramref name="isTheRightPage"/> is
    /// on screen: the screen, and a picture of each window of the setup on its own. Never fails the test, because it is
    /// for people (the pages that flash by, such as progress, may be gone before they are caught).
    /// </summary>
    public bool TryShow(string name, Func<Page, bool> isTheRightPage, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            var page = Snapshot();
            if (page != null && isTheRightPage(page))
            {
                Show(name);
                return true;
            }

            Thread.Sleep(100);
        }

        output.WriteLine($"page '{name}' was not on screen to photograph");
        return false;
    }

    /// <summary>A screenshot of the screen and of each setup window, named like the pages of a tour: <c>03-welcome.png</c>.</summary>
    public void Show(string name)
    {
        // A page has just appeared: let it finish drawing (and the flat UI style its window) before taking the picture.
        Thread.Sleep(600);
        var stem = Path.Combine(OutDir, $"{++dumps:00}-{name}");
        try
        {
            Capture.Screen().ToFile(stem + ".png");
            var index = 0;
            foreach (var window in Windows())
            {
                if (window.BoundingRectangle.IsEmpty)
                    continue;
                Capture.Element(window).ToFile($"{stem}.window{++index}.png");
            }

            output.WriteLine($"screenshot '{name}'");
        }
        catch (Exception e)
        {
            output.WriteLine($"could not take a screenshot of '{name}': {e.Message}");
        }
    }

    /// <summary>
    /// What a person debugging a missing window would want: every top-level window on the desktop with its process, whether the
    /// started process is still running, and the end of the log the setup wrote.
    /// </summary>
    public string Diagnostics(string? logName = null)
    {
        var text = new StringBuilder();
        text.AppendLine($"started process {process.Id} exited={process.HasExited}" + (process.HasExited ? $" code={process.ExitCode}" : ""));
        try
        {
            foreach (var window in automation.GetDesktop().FindAllChildren())
            {
                var pid = window.Properties.ProcessId.ValueOrDefault;
                string name;
                try
                {
                    using var p = Process.GetProcessById(pid);
                    name = p.ProcessName;
                }
                catch (Exception)
                {
                    name = "?";
                }

                text.AppendLine($"window '{SafeName(window)}' class='{Safe(() => window.ClassName)}' process={pid} ({name})");
            }
        }
        catch (Exception e)
        {
            text.AppendLine($"could not list the windows: {e.Message}");
        }

        try
        {
            foreach (var log in logFile != null ? new[] { logFile } : Directory.GetFiles(OutDir, (logName ?? "*") + "*.log"))
            {
                text.AppendLine($"--- {Path.GetFileName(log)} (last lines)");
                using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                foreach (var line in reader.ReadToEnd().Split('\n').TakeLast(60))
                    text.AppendLine(line.TrimEnd('\r'));
            }
        }
        catch (Exception e)
        {
            text.AppendLine($"could not read the log: {e.Message}");
        }

        return text.ToString();
    }

    /// <summary>Writes the UI Automation tree of every window of the setup and a screenshot.</summary>
    public void Dump(string name)
    {
        var stem = Path.Combine(OutDir, $"{++dumps:00}-{name}");
        try
        {
            var text = new StringBuilder();
            foreach (var window in Windows())
                Describe(window, 0, text);
            File.WriteAllText(stem + ".tree.txt", text.Length > 0 ? text.ToString() : "(no windows)");
            output.WriteLine($"UI tree of '{name}':{Environment.NewLine}{text}");
        }
        catch (Exception e)
        {
            output.WriteLine($"could not read the UI tree: {e.Message}");
        }

        try
        {
            Capture.Screen().ToFile(stem + ".png");
        }
        catch (Exception e)
        {
            output.WriteLine($"could not take a screenshot: {e.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // Already gone.
        }

        automation.Dispose();
        process.Dispose();
    }

    // The windows msiexec shows: the wizard and its spawned dialogs (such as "Are you sure you want to cancel?")
    // are all top-level windows of the msiexec process.
    IEnumerable<AutomationElement> Windows()
    {
        try
        {
            return automation.GetDesktop().FindAllChildren()
                .Where(w => BelongsToSetup(w.Properties.ProcessId.ValueOrDefault))
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<AutomationElement>();
        }
    }

    bool BelongsToSetup(int processId)
    {
        if (processId == process.Id)
            return true;
        if (processNames.Length == 0)
            return false;

        try
        {
            using var other = Process.GetProcessById(processId);
            return processNames.Any(n => string.Equals(other.ProcessName, n, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    IEnumerable<AutomationElement> Buttons()
    {
        foreach (var window in Windows())
        {
            IEnumerable<AutomationElement> all;
            try
            {
                all = window.FindAllDescendants();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var element in all)
            {
                if (SafeControlType(element) == ControlType.Button)
                    yield return element;
            }
        }
    }

    Page? Snapshot()
    {
        var names = new List<string>();
        var buttons = new List<string>();
        foreach (var window in Windows())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(window.Name))
                    names.Add(window.Name);
                foreach (var element in window.FindAllDescendants())
                {
                    var name = SafeName(element);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    names.Add(name);
                    if (SafeControlType(element) == ControlType.Button)
                        buttons.Add(name);
                }
            }
            catch (Exception)
            {
                // The page was replaced while we were reading it: try again.
                return null;
            }
        }

        return names.Count == 0 ? null : new Page(names, buttons);
    }

    static void Describe(AutomationElement element, int depth, StringBuilder text)
    {
        text.Append(' ', depth * 2)
            .Append(SafeControlType(element)).Append(" name='").Append(SafeName(element)).Append("'")
            .Append(" id='").Append(Safe(() => element.AutomationId)).Append("'")
            .Append(" class='").Append(Safe(() => element.ClassName)).Append("'")
            .Append(" enabled=").Append(Safe(() => element.IsEnabled.ToString()));

        // What an input box holds is what the tests are about, so the tree shows it.
        if (SafeControlType(element) == ControlType.Edit)
            text.Append(" value='").Append(Safe(() => ReadText(element))).Append("'");
        text.AppendLine();

        AutomationElement[] children;
        try
        {
            children = element.FindAllChildren();
        }
        catch (Exception)
        {
            return;
        }

        foreach (var child in children)
            Describe(child, depth + 1, text);
    }

    static T? Retry<T>(Func<T?> find, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            try
            {
                var found = find();
                if (found != null)
                    return found;
            }
            catch (Exception)
            {
                // The element went away between finding and reading it.
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    static IEnumerable<AutomationElement> EditsOf(AutomationElement window)
    {
        try
        {
            return window.FindAllDescendants()
                .Where(e => SafeControlType(e) == ControlType.Edit && Safe(() => e.ClassName) == "RichEdit20W")
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<AutomationElement>();
        }
    }

    static string SafeName(AutomationElement element) => Safe(() => element.Name);

    static ControlType SafeControlType(AutomationElement element)
    {
        try
        {
            return element.ControlType;
        }
        catch (Exception)
        {
            return ControlType.Unknown;
        }
    }

    static string Safe(Func<string?> read)
    {
        try
        {
            return read() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    // Button labels carry mnemonic marks and arrows ("&Next >"): compare without them.
    static bool Same(string label, string wanted) => Normalize(label) == Normalize(wanted);

    static string Normalize(string label) => new string(label.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
