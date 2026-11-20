using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.UI.Forms;
using WixSharp.UI.WPF;
using WixToolset.Dtf.WindowsInstaller;
using Wpf = System.Windows.Controls;

namespace InstallKit.WixSharp.Flat
{
    /// <summary>
    /// The first page: product, the license (when set: the accept box above the text, Next waits for it), and the button that
    /// starts the install or goes on.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatWelcomeDialog : FlatDialog
    {
        protected override void Build()
        {
            TitleBlock.Text = ProductName;
            SubtitleBlock.Text = "Version " + Session.Property("ProductVersion") + "  -  " + Session.Property("Manufacturer");

            CheckBox accept = null;

            string license = null;
            try
            {
                if (Session.Property(FlatKeys.License) == "1")
                    license = Session.GetResourceString("WixSharp_LicenceFile");
            }
            catch (Exception)
            {
                // No license in this MSI.
            }

            var panel = new DockPanel();

            var intro = Label("Setup will install " + ProductName + " on this computer.");
            intro.Margin = new Thickness(0, 0, 0, string.IsNullOrWhiteSpace(license) ? 0 : 14);
            DockPanel.SetDock(intro, Dock.Top);
            panel.Children.Add(intro);

            if (!string.IsNullOrWhiteSpace(license))
            {
                // The accept box on the left; on the right two buttons to read the license in a program of the user's choice or keep a copy.
                accept = new CheckBox { Content = "I accept the license terms", VerticalAlignment = VerticalAlignment.Center };
                var open = new Wpf.Button { Content = "Open license", Padding = new Thickness(10, 2, 10, 2) };
                var save = new Wpf.Button { Content = "Save a copy...", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
                open.Click += (sender, args) => OpenLicense(license);
                save.Click += (sender, args) => SaveLicense(license);

                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
                var actions = new StackPanel { Orientation = Orientation.Horizontal };
                actions.Children.Add(open);
                actions.Children.Add(save);
                DockPanel.SetDock(actions, Dock.Right);
                row.Children.Add(actions);
                row.Children.Add(accept);
                DockPanel.SetDock(row, Dock.Top);
                panel.Children.Add(row);

                // The license is rich text with its own colours: show it on a light card in every theme. It takes the rest of the page.
                var box = new RichTextBox
                {
                    IsReadOnly = true,
                    Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                    BorderBrush = Paint("Line"),
                    Padding = new Thickness(8),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                };
                ShowRtf(box, license);
                panel.Children.Add(box);
            }

            Body.Children.Add(panel);

            AddButton("Cancel", () => ShellApi.Cancel());
            var go = AddButton(PagesAfterWelcome.Length > 0 ? "Next" : "Install", () => ShellApi.GoNext(), primary: true);
            if (accept != null)
            {
                go.IsEnabled = false;
                accept.Checked += (sender, args) => go.IsEnabled = true;
                accept.Unchecked += (sender, args) => go.IsEnabled = false;
            }
        }

        string LicenseFileName => string.Join("_", (ProductName + " license").Split(Path.GetInvalidFileNameChars())) + ".rtf";

        // Opens the license in whatever the user has for .rtf files (WordPad, Word): to read it larger, search it or print it.
        void OpenLicense(string rtf)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), LicenseFileName);
                System.IO.File.WriteAllBytes(path, Encoding.UTF8.GetBytes(rtf));
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // No program for .rtf files: the license is still readable (and can be saved) on this page.
            }
        }

        void SaveLicense(string rtf)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { FileName = LicenseFileName, DefaultExt = ".rtf", Filter = "Rich Text Format (*.rtf)|*.rtf" };
                if (dialog.ShowDialog() == true)
                    System.IO.File.WriteAllBytes(dialog.FileName, Encoding.UTF8.GetBytes(rtf));
            }
            catch (Exception)
            {
                // The folder cannot be written to: nothing is lost, the user can pick another one.
            }
        }

        static void ShowRtf(RichTextBox box, string rtf)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtf)))
            {
                box.SelectAll();
                box.Selection.Load(stream, DataFormats.Rtf);
            }
        }
    }

    /// <summary>The install folder with Browse, and the values the product asks for (prompted properties): a label and a box each.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatSettingsDialog : FlatDialog
    {
        protected override void Build()
        {
            var dirProperty = Session.Property("WixSharp_UI_INSTALLDIR");
            var hasFolder = !string.IsNullOrEmpty(dirProperty);

            TitleBlock.Text = "Settings";
            SubtitleBlock.Text = hasFolder ? "Choose where to install, and tell setup a little about this installation." : "Tell setup a little about this installation.";

            var prompts = (Session.Property(FlatKeys.Prompts) ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split('|'))
                .Where(parts => parts.Length == 3)
                .Select(parts => new { Property = parts[0], Label = Uri.UnescapeDataString(parts[1]), Required = parts[2] == "1" })
                .ToList();

            var panel = new StackPanel();
            if (hasFolder)
                AddFolder(panel, dirProperty);

            var required = new List<TextBox>();
            foreach (var prompt in prompts)
            {
                panel.Children.Add(Label(prompt.Label, muted: true));
                var box = new TextBox { Text = Session.Property(prompt.Property), Margin = new Thickness(0, 6, 0, 18) };
                var property = prompt.Property;
                box.TextChanged += (sender, args) => Session[property] = box.Text;
                panel.Children.Add(box);
                if (prompt.Required)
                    required.Add(box);
            }

            Body.Children.Add(panel);

            AddButton("Back", () => ShellApi.GoPrev());
            AddButton("Cancel", () => ShellApi.Cancel());
            var go = AddButton(PagesAfterWelcome.Contains("features") ? "Next" : "Install", () => ShellApi.GoNext(), primary: true);

            void Refresh() => go.IsEnabled = required.All(box => box.Text.Length > 0);
            foreach (var box in required)
                box.TextChanged += (sender, args) => Refresh();
            Refresh();
        }

        void AddFolder(StackPanel panel, string dirProperty)
        {
            panel.Children.Add(Label("Install to:", muted: true));

            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 18) };
            var browse = new Wpf.Button { Content = "Browse...", Margin = new Thickness(10, 0, 0, 0), MinWidth = 90 };
            var path = new TextBox { Text = InstallDir(dirProperty) };
            path.TextChanged += (sender, args) => Session[dirProperty] = path.Text;
            browse.Click += (sender, args) =>
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = path.Text })
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        path.Text = dialog.SelectedPath;
                }
            };
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            row.Children.Add(path);
            panel.Children.Add(row);
        }

        // At this point the install directory property may not be resolved yet (the same dance as WixSharp's own folder page).
        string InstallDir(string dirProperty)
        {
            var value = Session.Property(dirProperty);
            if (!string.IsNullOrEmpty(value))
                return value;

            var directory = Session.GetDirectoryPath(dirProperty);
            return directory == "ABSOLUTEPATH" ? Session.Property("INSTALLDIR_ABSOLUTEPATH") : directory;
        }
    }

    /// <summary>The optional features, one check box each (indented below their parent).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatFeaturesDialog : FlatDialog
    {
        sealed class Row
        {
            public FeatureItem Item;
            public CheckBox Box;
        }

        readonly List<Row> rows = new List<Row>();

        protected override void Build()
        {
            var changing = Session.Property("MODIFY_ACTION") == "Change";
            TitleBlock.Text = "Features";
            SubtitleBlock.Text = "Choose what to " + (changing ? "change" : "install") + ".";

            // A copy: the features of the session cannot be modified.
            var features = Session.Features;
            var depth = new Dictionary<string, int>();
            foreach (var feature in features)
            {
                var level = 0;
                for (var parent = feature.ParentName; !string.IsNullOrEmpty(parent); level++)
                    parent = features.FirstOrDefault(f => f.Name == parent)?.ParentName;
                depth[feature.Name] = level;
            }

            var panel = new StackPanel();
            foreach (var feature in Flatten(features))
            {
                var row = new Row { Item = feature };
                rows.Add(row);
                if (feature.Display == FeatureDisplay.hidden)
                    continue;

                var content = new StackPanel();
                content.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(feature.Title) ? feature.Name : feature.Title });
                if (!string.IsNullOrEmpty(feature.Description))
                    content.Children.Add(new TextBlock { Text = feature.Description, Foreground = Paint("Muted"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });

                row.Box = new CheckBox
                {
                    Content = content,
                    IsChecked = feature.DefaultIsToBeInstalled(),
                    IsEnabled = !feature.DisallowAbsent,
                    Margin = new Thickness(depth[feature.Name] * 24, 0, 0, 9),
                };
                panel.Children.Add(row.Box);
            }

            Body.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            AddButton("Back", () => ShellApi.GoPrev());
            AddButton("Cancel", () => ShellApi.Cancel());
            AddButton(changing ? "Change" : "Install", GoNext, primary: true);
        }

        // Parents before their children, each parent's children directly after it.
        static IEnumerable<FeatureItem> Flatten(FeatureItem[] features)
        {
            IEnumerable<FeatureItem> Below(string parent)
            {
                foreach (var feature in features.Where(f => (f.ParentName ?? string.Empty) == parent))
                {
                    yield return feature;
                    foreach (var child in Below(feature.Name))
                        yield return child;
                }
            }

            return Below(string.Empty).ToList();
        }

        /*
         * ADDLOCAL: the features to install, REMOVE: the ones to remove. Only set when the user changed something,
         * so an untouched page leaves the defaults (and a repair or upgrade) alone.
         */
        void GoNext()
        {
            bool Wanted(Row row) => row.Box != null ? row.Box.IsChecked == true : row.Item.DefaultIsToBeInstalled();

            var changed = rows.Any(row => Wanted(row) != row.Item.DefaultIsToBeInstalled());
            if (changed)
            {
                var install = string.Join(",", rows.Where(Wanted).Select(row => row.Item.Name));
                var remove = string.Join(",", rows.Where(row => !Wanted(row)).Select(row => row.Item.Name));
                if (remove.Length > 0)
                    Session["REMOVE"] = remove;
                if (install.Length > 0)
                    Session["ADDLOCAL"] = install;
            }
            else
            {
                Session["REMOVE"] = "";
                Session["ADDLOCAL"] = "";
            }

            ShellApi.GoNext();
        }
    }

    /// <summary>Shown when the installed product is run again: change, repair or remove.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatMaintenanceDialog : FlatDialog
    {
        protected override void Build()
        {
            TitleBlock.Text = ProductName;
            SubtitleBlock.Text = "What would you like to do?";

            var panel = new StackPanel();
            if (PagesAfterWelcome.Contains("features"))
            {
                Add(panel, "Change", "Choose which features are installed.", () =>
                {
                    Session["MODIFY_ACTION"] = "Change";
                    ShellApi.GoNext();
                });
            }

            Add(panel, "Repair", "Fix missing or damaged files.", () =>
            {
                Session["MODIFY_ACTION"] = "Repair";
                JumpToProgress();
            });
            Add(panel, "Remove", "Uninstall " + ProductName + " from this computer.", () =>
            {
                Session["REMOVE"] = "ALL";
                Session["MODIFY_ACTION"] = "Remove";
                JumpToProgress();
            });

            Body.Children.Add(panel);
            AddButton("Cancel", () => ShellApi.Cancel());
        }

        void Add(StackPanel panel, string text, string description, System.Action click)
        {
            // The button and what it does on one row, so three of them fit the page.
            var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var button = new Wpf.Button { Content = text, MinWidth = 130, HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += (sender, args) => click();
            row.Children.Add(button);

            var label = new TextBlock { Text = description, Foreground = Paint("Muted"), Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            panel.Children.Add(row);
        }

        // Repair and remove skip the pages in between.
        void JumpToProgress()
        {
            var index = ShellApi.Dialogs.IndexOfDialogImplementing<IProgressDialog>();
            if (index != -1)
                ShellApi.GoTo(index);
            else
                ShellApi.GoNext();
        }
    }

    /// <summary>Progress of the install, repair or removal.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatProgressDialog : FlatDialog, IProgressDialog
    {
        ProgressBar bar;
        TextBlock action;
        TextBlock description;

        protected override void Build()
        {
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            description = Label(string.Empty);
            description.Margin = new Thickness(0, 0, 0, 22);
            bar = new ProgressBar { Minimum = 0, Maximum = 100, Margin = new Thickness(0, 0, 0, 12) };
            action = Label(string.Empty, muted: true, size: 12);
            panel.Children.Add(description);
            panel.Children.Add(bar);
            panel.Children.Add(action);
            Body.Children.Add(panel);

            UpdateTitles(Session);
            AddButton("Cancel", () =>
            {
                if (ShellApi.IsDemoMode)
                    ShellApi.GoNext();
                else
                    ShellApi.Cancel();
            });

            ShellApi.StartExecute();
        }

        public void UpdateTitles(ISession session)
        {
            if (session.IsUninstalling())
            {
                TitleBlock.Text = "Removing";
                description.Text = "Please wait while " + ProductName + " is removed from this computer.";
            }
            else if (session.IsRepairing())
            {
                TitleBlock.Text = "Repairing";
                description.Text = "Please wait while " + ProductName + " is repaired.";
            }
            else
            {
                TitleBlock.Text = "Installing";
                description.Text = "Please wait while " + ProductName + " is installed.";
            }

            SubtitleBlock.Text = string.Empty;
        }

        public override MessageResult ProcessMessage(InstallMessage messageType, Record messageRecord, MessageButtons buttons, MessageIcon icon, MessageDefaultButton defaultButton)
        {
            if (messageType == InstallMessage.ActionStart)
            {
                try
                {
                    // Field 2 is the text of the action ("Copying new files").
                    var text = messageRecord.FieldCount >= 3 ? messageRecord[2].ToString() : string.Empty;
                    Dispatcher.BeginInvoke(new System.Action(() => action.Text = text));
                }
                catch (Exception)
                {
                    // Never let a message crash the install.
                }
            }

            return MessageResult.OK;
        }

        public override void OnProgress(int progressPercentage)
            => Dispatcher.BeginInvoke(new System.Action(() => bar.Value = progressPercentage));

        public override void OnExecuteComplete()
            => Dispatcher.BeginInvoke(new System.Action(() =>
            {
                action.Text = string.Empty;
                ShellApi.GoNext();
            }));
    }

    /// <summary>The last page: done, interrupted or failed.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FlatExitDialog : FlatDialog
    {
        protected override void Build()
        {
            var interrupted = ShellApi.UserInterrupted || ShellApi.Log.Contains("User canceled installation.");
            var failed = !interrupted && ShellApi.ErrorDetected;

            TitleBlock.Text = interrupted ? "Setup interrupted" : failed ? "Setup failed" : "All set";
            SubtitleBlock.Text = ProductName;

            var text = interrupted ? "Your system has not been modified. To install this program at a later time, run Setup again."
                : failed ? (ShellApi.CustomErrorDescription ?? "Setup ended prematurely because of an error. Your system has not been modified.")
                : "Setup finished. Click Finish to close this window.";
            Body.Children.Add(new StackPanel { Children = { Label(text) } });

            if (failed || interrupted)
                AddButton("View log", ViewLog, left: true);
            AddButton("Finish", () => ShellApi.Exit(), primary: true);
        }

        void ViewLog()
        {
            try
            {
                var logFile = Session.LogFile;
                if (string.IsNullOrEmpty(logFile))
                {
                    logFile = Path.Combine(Path.GetTempPath(), ManagedFormHost.Runtime.ProductName + ".log");
                    System.IO.File.WriteAllText(logFile, ShellApi.Log);
                }

                Process.Start("notepad.exe", logFile);
            }
            catch (Exception)
            {
                // Viewing the log must never crash the setup.
            }
        }
    }
}
