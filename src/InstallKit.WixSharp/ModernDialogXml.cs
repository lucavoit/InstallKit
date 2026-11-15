using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace InstallKit.WixSharp
{
    /// <summary>What the modern dialogs need to know about the product. Plain data, so the XML builder has no WixSharp dependency.</summary>
    internal sealed class ModernDialogOptions
    {
        /// <summary>Full path of an RTF license shown on the welcome page, or <c>null</c>.</summary>
        public string? LicenseFile { get; set; }

        /// <summary>Show the install folder and a Browse button on the welcome page.</summary>
        public bool ChooseInstallDir { get; set; }

        /// <summary>
        /// The product has optional features: the welcome page leads to a feature page, and the repair dialog gets a
        /// Change button that leads there too.
        /// </summary>
        public bool HasFeatures { get; set; }

        /// <summary>Values to ask for on a page of their own after the welcome page (at most three fit).</summary>
        public IReadOnlyList<ModernPrompt> Prompts { get; set; } = new ModernPrompt[0];
    }

    /// <summary>One input box on the settings page.</summary>
    internal sealed class ModernPrompt
    {
        public ModernPrompt(string property, string label, bool required)
        {
            Property = property;
            Label = label;
            Required = required;
        }

        public string Property { get; }

        public string Label { get; }

        public bool Required { get; }
    }

    /// <summary>
    /// Builds the WiX UI fragment of the native modern dialog set (see ModernDialogs for what it looks like).
    /// It only produces XML and uses no WixSharp types, so it can be unit tested.
    /// </summary>
    internal static class ModernDialogXml
    {
        public const string UiId = "InstallKit_Modern";
        public const string WelcomeId = "InstallKit_WelcomeDlg";
        public const string FinishId = "InstallKit_FinishDlg";
        public const string OptionsId = "InstallKit_OptionsDlg";

        // Dialog units. 370x270 is the standard WiX dialog size; the background bitmap covers 370x234.
        const int Width = 370;
        const int Height = 270;
        const int ContentX = 135; // right of the 164 px accent panel in the generated background
        const int ContentWidth = 220;

        public static XElement BuildFragment(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var features = options.HasFeatures;
            var prompts = options.Prompts.Count > 0;
            // The page before the feature page when installing: the settings page, or the welcome page.
            var beforeFeatures = prompts ? OptionsId : WelcomeId;

            var dialogRefs = new List<string>
            {
                "ErrorDlg", "FatalError", "FilesInUse", "MsiRMFilesInUse", "PrepareDlg", "ProgressDlg", "ResumeDlg",
                "UserExit", "CancelDlg", "OutOfDiskDlg", "MaintenanceWelcomeDlg", "MaintenanceTypeDlg", "VerifyReadyDlg",
            };
            if (options.ChooseInstallDir || features)
                dialogRefs.Add("BrowseDlg");
            if (features)
                dialogRefs.AddRange(new[] { "CustomizeDlg", "DiskCostDlg" });

            var ui = E("UI", A("Id", UiId),
                // Segoe UI everywhere, including the reused WiX dialogs.
                E("TextStyle", A("Id", "WixUI_Font_Normal"), A("FaceName", "Segoe UI"), A("Size", 9)),
                E("TextStyle", A("Id", "WixUI_Font_Bigger"), A("FaceName", "Segoe UI"), A("Size", 12)),
                E("TextStyle", A("Id", "WixUI_Font_Title"), A("FaceName", "Segoe UI Semibold"), A("Size", 9)),
                E("TextStyle", A("Id", "InstallKit_Font_Hero"), A("FaceName", "Segoe UI Light"), A("Size", 20)),
                E("TextStyle", A("Id", "InstallKit_Font_Muted"), A("FaceName", "Segoe UI"), A("Size", 9), A("Red", 96), A("Green", 96), A("Blue", 96)),
                E("Property", A("Id", "DefaultUIFont"), A("Value", "WixUI_Font_Normal")),
                // The mode decides which buttons the repair dialog shows: Change is only there for the feature tree.
                E("Property", A("Id", "WixUI_Mode"), A("Value", features ? "FeatureTree" : "Minimal")),
                features ? null : E("Property", A("Id", "ARPNOMODIFY"), A("Value", "1")),

                // Standard dialogs we reuse as-is.
                dialogRefs.Distinct().Select(id => E("DialogRef", A("Id", id))),

                // The folder browser has an OK button without events: the dialog set that uses it has to give it some
                // (WixUI_InstallDir and WixUI_FeatureTree do the same), or the button does nothing (ICE17).
                options.ChooseInstallDir || features
                    ? new[]
                    {
                        Publish(ns, "BrowseDlg", "OK", "SetTargetPath", "[_BrowseProperty]", 3),
                        Publish(ns, "BrowseDlg", "OK", "EndDialog", "Return", 4),
                    }
                    : null,

                // Repair / remove flow (as in WixUI_Minimal, and WixUI_FeatureTree for the feature page).
                Publish(ns, "MaintenanceWelcomeDlg", "Next", "NewDialog", "MaintenanceTypeDlg"),
                Publish(ns, "MaintenanceTypeDlg", "RepairButton", "NewDialog", "VerifyReadyDlg"),
                Publish(ns, "MaintenanceTypeDlg", "RemoveButton", "NewDialog", "VerifyReadyDlg"),
                Publish(ns, "MaintenanceTypeDlg", "Back", "NewDialog", "MaintenanceWelcomeDlg"),
                features
                    ? new[]
                    {
                        Publish(ns, "MaintenanceTypeDlg", "ChangeButton", "NewDialog", "CustomizeDlg"),
                        Publish(ns, "CustomizeDlg", "Back", "NewDialog", "MaintenanceTypeDlg", 1, "WixUI_InstallMode = \"Change\""),
                        Publish(ns, "CustomizeDlg", "Back", "NewDialog", beforeFeatures, 2, "WixUI_InstallMode <> \"Change\""),
                        Publish(ns, "CustomizeDlg", "Next", "NewDialog", "VerifyReadyDlg"),
                        Publish(ns, "VerifyReadyDlg", "Back", "NewDialog", "CustomizeDlg", 1, "NOT Installed OR WixUI_InstallMode = \"Change\""),
                        Publish(ns, "VerifyReadyDlg", "Back", "NewDialog", "MaintenanceTypeDlg", 2, "Installed AND WixUI_InstallMode <> \"Change\""),
                    }
                    : new[] { Publish(ns, "VerifyReadyDlg", "Back", "NewDialog", "MaintenanceTypeDlg") },

                Welcome(ns, options),
                prompts ? Options(ns, options) : null,
                Finish(ns),

                E("InstallUISequence",
                    E("Show", A("Dialog", WelcomeId), A("Before", "ProgressDlg"), A("Condition", "NOT Installed")),
                    // Running the MSI again on an installed product offers repair, remove and (with features) change. The
                    // DialogRefs above bring ResumeDlg and MaintenanceWelcomeDlg with their own Show entries, which WiX
                    // defines as virtual: scheduling them here too is a conflict (WIX7009), so we leave them to the extension.
                    E("Show", A("Dialog", FinishId), A("OnExit", "success"))),
                E("AdminUISequence",
                    E("Show", A("Dialog", FinishId), A("OnExit", "success"))));

            return E("Fragment", ui, E("UIRef", A("Id", "WixUI_Common")));
        }

        static XElement Welcome(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var license = options.LicenseFile;
            var chooseFolder = options.ChooseInstallDir;
            var features = options.HasFeatures;
            // The page this one leads to, or null when its button starts the install.
            var next = options.Prompts.Count > 0 ? OptionsId : features ? "CustomizeDlg" : null;

            var dialog = E("Dialog", A("Id", WelcomeId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}[ProductName]"),
                Text(ns, "Subtitle", 58, 14, "{\\InstallKit_Font_Muted}Version [ProductVersion]  -  [Manufacturer]"));

            var y = 82;

            if (license != null)
            {
                dialog.Add(
                    E("Control", A("Id", "License"), A("Type", "ScrollableText"), A("X", ContentX), A("Y", y), A("Width", ContentWidth),
                      A("Height", chooseFolder ? 70 : 100), A("Sunken", "yes"), A("TabSkip", "no"),
                      E("Text", A("SourceFile", license))));
                y += chooseFolder ? 74 : 104;

                dialog.Add(
                    E("Control", A("Id", "Accept"), A("Type", "CheckBox"), A("X", ContentX), A("Y", y), A("Width", ContentWidth), A("Height", 14),
                      A("Property", "InstallKitLicenseAccepted"), A("CheckBoxValue", "1"), A("Text", "I accept the license terms")));
                y += 20;
            }
            else
            {
                dialog.Add(Text(ns, "Intro", y, 40, next != null
                    ? "Setup will install [ProductName] on this computer. Click Next to continue."
                    : "Setup will install [ProductName] on this computer. Click Install to continue."));
                y += 46;
            }

            if (chooseFolder)
            {
                dialog.Add(
                    Text(ns, "FolderLabel", y, 12, "Install to:"),
                    E("Control", A("Id", "Folder"), A("Type", "PathEdit"), A("X", ContentX), A("Y", y + 14), A("Width", ContentWidth - 62), A("Height", 17),
                      A("Property", "INSTALLDIR"), A("Indirect", "no")),
                    E("Control", A("Id", "Browse"), A("Type", "PushButton"), A("X", ContentX + ContentWidth - 56), A("Y", y + 14), A("Width", 56), A("Height", 17),
                      A("Text", "Browse..."),
                      E("Publish", A("Property", "_BrowseProperty"), A("Value", "INSTALLDIR"), A("Order", 1)),
                      E("Publish", A("Event", "SpawnDialog"), A("Value", "BrowseDlg"), A("Order", 2))));
            }

            // Without features this button starts the install. With features it leads to the feature page, and the
            // "Ready to install" page after it (which has the elevation shield and the disk space check) starts it.
            var install = E("Control", A("Id", "Install"), A("Type", "PushButton"), A("X", 236), A("Y", 243), A("Width", 56), A("Height", 17),
                A("Default", "yes"), next != null ? null : A("ElevationShield", "yes"), A("Text", next != null ? "Next" : "Install"),
                chooseFolder ? E("Publish", A("Event", "SetTargetPath"), A("Value", "INSTALLDIR"), A("Order", 1)) : null,
                next != null
                    ? new[] { E("Publish", A("Event", "NewDialog"), A("Value", next), A("Order", 2)) }
                    : new[]
                    {
                        E("Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Order", 2), A("Condition", "OutOfDiskSpace = 1")),
                        E("Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Order", 3), A("Condition", "OutOfDiskSpace <> 1")),
                    });

            if (license != null)
            {
                install.Add(A("DisableCondition", "InstallKitLicenseAccepted <> \"1\""), A("EnableCondition", "InstallKitLicenseAccepted = \"1\""));
            }

            dialog.Add(
                E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)),
                install,
                E("Control", A("Id", "Cancel"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Cancel", "yes"), A("Text", "Cancel"),
                  E("Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));

            return dialog;
        }

        /// <summary>The page with the prompted values: one label and input box each, then Back and Next (or Install).</summary>
        static XElement Options(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var features = options.HasFeatures;

            var dialog = E("Dialog", A("Id", OptionsId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}Settings"),
                Text(ns, "Subtitle", 58, 14, "{\\InstallKit_Font_Muted}Tell setup a little about this installation."));

            var y = 82;
            for (var i = 0; i < options.Prompts.Count; i++)
            {
                var prompt = options.Prompts[i];
                dialog.Add(
                    Text(ns, $"PromptLabel{i + 1}", y, 12, prompt.Label),
                    E("Control", A("Id", $"Prompt{i + 1}"), A("Type", "Edit"), A("X", ContentX), A("Y", y + 14), A("Width", ContentWidth), A("Height", 17),
                      A("Property", prompt.Property), A("Sunken", "yes")));
                y += 40;
            }

            var required = options.Prompts.Where(p => p.Required).Select(p => $"{p.Property} = \"\"").ToList();
            var forward = E("Control", A("Id", "Next"), A("Type", "PushButton"), A("X", 236), A("Y", 243), A("Width", 56), A("Height", 17),
                A("Default", "yes"), features ? null : A("ElevationShield", "yes"), A("Text", features ? "Next" : "Install"),
                features
                    ? new[] { E("Publish", A("Event", "NewDialog"), A("Value", "CustomizeDlg"), A("Order", 1)) }
                    : new[]
                    {
                        E("Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Order", 1), A("Condition", "OutOfDiskSpace = 1")),
                        E("Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Order", 2), A("Condition", "OutOfDiskSpace <> 1")),
                    });
            if (required.Count > 0)
                forward.Add(A("DisableCondition", string.Join(" OR ", required)));

            dialog.Add(
                E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)),
                E("Control", A("Id", "Back"), A("Type", "PushButton"), A("X", 180), A("Y", 243), A("Width", 56), A("Height", 17), A("Text", "Back"),
                  E("Publish", A("Event", "NewDialog"), A("Value", WelcomeId))),
                forward,
                E("Control", A("Id", "Cancel"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Cancel", "yes"), A("Text", "Cancel"),
                  E("Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));

            return dialog;
        }

        static XElement Finish(XNamespace ns)
        {
            XElement E(string name, params object[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            return E("Dialog", A("Id", FinishId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}All set"),
                Text(ns, "Description", 62, 40, "Setup finished. Click Finish to close this window."),
                E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)),
                E("Control", A("Id", "Finish"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Default", "yes"), A("Cancel", "yes"), A("Text", "Finish"),
                  E("Publish", A("Event", "EndDialog"), A("Value", "Return"))));
        }

        static XElement Bitmap(XNamespace ns) =>
            new XElement(ns + "Control",
                new XAttribute("Id", "Background"), new XAttribute("Type", "Bitmap"),
                new XAttribute("X", 0), new XAttribute("Y", 0), new XAttribute("Width", Width), new XAttribute("Height", 234),
                new XAttribute("TabSkip", "no"), new XAttribute("Text", "WixUI_Bmp_Dialog"));

        static XElement Text(XNamespace ns, string id, int y, int height, string text) =>
            new XElement(ns + "Control",
                new XAttribute("Id", id), new XAttribute("Type", "Text"),
                new XAttribute("X", ContentX), new XAttribute("Y", y), new XAttribute("Width", ContentWidth), new XAttribute("Height", height),
                new XAttribute("Transparent", "yes"), new XAttribute("NoPrefix", "yes"), new XAttribute("Text", text));

        static XElement Publish(XNamespace ns, string dialog, string control, string @event, string value, int? order = null, string? condition = null) =>
            new XElement(ns + "Publish",
                new XAttribute("Dialog", dialog), new XAttribute("Control", control),
                new XAttribute("Event", @event), new XAttribute("Value", value),
                order == null ? null : new XAttribute("Order", order),
                condition == null ? null : new XAttribute("Condition", condition));
    }
}
