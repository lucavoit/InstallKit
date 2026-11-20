using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace InstallKit.WixSharp
{
    /// <summary>What the modern dialogs need to know about the product. Plain data, so the XML builder has no WixSharp dependency.</summary>
    internal sealed class ModernDialogOptions
    {
        /// <summary>Full path of an RTF license shown on the welcome page (accept box above it), or <c>null</c>.</summary>
        public string? LicenseFile { get; set; }

        /// <summary>Show the install folder and a Browse button, on the page after the welcome page (above any prompted values).</summary>
        public bool ChooseInstallDir { get; set; }

        /// <summary>
        /// The product has optional features: the welcome page leads to a feature page, and the repair dialog gets a
        /// Change button that leads there too.
        /// </summary>
        public bool HasFeatures { get; set; }

        /// <summary>Colours of the pages. Defaults to the dark theme.</summary>
        public Palette Palette { get; set; } = Palette.Create(light: false, accent: null, background: null, text: null);

        /// <summary>
        /// Full path of the full-page background (370x270 dialog units) the modern pages sit on, or <c>null</c> to use the
        /// stock dialog bitmap. It also lets the footer with the buttons share the page's colour.
        /// </summary>
        public string? PageBackground { get; set; }

        /// <summary>
        /// Full path of the 260x85 dialog unit background of the cancel prompt, or <c>null</c> to leave that dialog stock.
        /// </summary>
        public string? CancelBackground { get; set; }

        /// <summary>Values to ask for on the page after the welcome page, below the install folder (at most three fit with the folder).</summary>
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
        public const string PageBitmapId = "IK_Bmp_Page";
        public const string CancelBitmapId = "IK_Bmp_Cancel";

        // Dialog units. 370x270 is the standard WiX dialog size; the background bitmap covers 370x234.
        internal const int Width = 370;
        internal const int Height = 270;
        internal const int ContentX = 135; // right of the 164 px accent panel in the generated background
        internal const int ContentWidth = 220;

        public static XElement BuildFragment(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var features = options.HasFeatures;
            string D(string wixId) => StockDialogXml.Id(options, wixId);
            var hasOptions = HasOptionsPage(options);
            // The page before the feature page when installing: the install folder / settings page, or the welcome page.
            var beforeFeatures = hasOptions ? OptionsId : WelcomeId;

            // With a page background the pages of the install and repair flows are our own (see StockDialogXml); the
            // rest, popups and the folder and feature pages, stay WiX's.
            var skin = StockDialogXml.Skins(options);
            var dialogRefs = new List<string>
            {
                "ErrorDlg", "FilesInUse", "MsiRMFilesInUse", "PrepareDlg", "OutOfDiskDlg", "OutOfRbDiskDlg", "WaitForCostingDlg",
                "FatalError", "ProgressDlg", "ResumeDlg", "UserExit", "CancelDlg", "MaintenanceWelcomeDlg", "MaintenanceTypeDlg", "VerifyReadyDlg",
            }.Where(id => !skin.Contains(id)).ToList();
            if (options.ChooseInstallDir || features)
                dialogRefs.Add("BrowseDlg");
            if (features)
                dialogRefs.AddRange(new[] { "CustomizeDlg", "DiskCostDlg" });

            var ui = E("UI", A("Id", UiId),
                // Segoe UI everywhere, including the reused WiX dialogs.
                E("TextStyle", A("Id", "WixUI_Font_Normal"), A("FaceName", "Segoe UI"), A("Size", 9)),
                E("TextStyle", A("Id", "WixUI_Font_Bigger"), A("FaceName", "Segoe UI"), A("Size", 12)),
                E("TextStyle", A("Id", "WixUI_Font_Title"), A("FaceName", "Segoe UI Semibold"), A("Size", 9)),
                // Our own pages sit on the generated page background; the reused dialogs keep the stock (light) body.
                // The title colour is the text colour pulled slightly towards the background: Windows Installer drew a title in an
                // almost white colour (243,244,246) black, while the same style with (216,217,219) was fine.
                E("TextStyle", A("Id", "InstallKit_Font_Hero"), A("FaceName", "Segoe UI Semibold"), A("Size", 22), Colour(options.Palette.Text.Mix(options.Palette.Background, 0.08))),


                E("TextStyle", A("Id", "InstallKit_Font_Body"), A("FaceName", "Segoe UI"), A("Size", 9), Colour(options.Palette.Text.Mix(options.Palette.Background, 0.12))),
                E("TextStyle", A("Id", "InstallKit_Font_Muted"), A("FaceName", "Segoe UI"), A("Size", 9), Colour(options.Palette.Muted)),
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
                Publish(ns, D("MaintenanceWelcomeDlg"), "Next", "NewDialog", D("MaintenanceTypeDlg")),
                Publish(ns, D("MaintenanceTypeDlg"), "RepairButton", "NewDialog", D("VerifyReadyDlg")),
                Publish(ns, D("MaintenanceTypeDlg"), "RemoveButton", "NewDialog", D("VerifyReadyDlg")),
                Publish(ns, D("MaintenanceTypeDlg"), "Back", "NewDialog", D("MaintenanceWelcomeDlg")),
                features
                    ? new[]
                    {
                        Publish(ns, D("MaintenanceTypeDlg"), "ChangeButton", "NewDialog", "CustomizeDlg"),
                        Publish(ns, "CustomizeDlg", "Back", "NewDialog", D("MaintenanceTypeDlg"), 1, "WixUI_InstallMode = \"Change\""),
                        Publish(ns, "CustomizeDlg", "Back", "NewDialog", beforeFeatures, 2, "WixUI_InstallMode <> \"Change\""),
                        Publish(ns, "CustomizeDlg", "Next", "NewDialog", D("VerifyReadyDlg")),
                        Publish(ns, D("VerifyReadyDlg"), "Back", "NewDialog", "CustomizeDlg", 1, "NOT Installed OR WixUI_InstallMode = \"Change\""),
                        Publish(ns, D("VerifyReadyDlg"), "Back", "NewDialog", D("MaintenanceTypeDlg"), 2, "Installed AND WixUI_InstallMode <> \"Change\""),
                    }
                    : new[] { Publish(ns, D("VerifyReadyDlg"), "Back", "NewDialog", D("MaintenanceTypeDlg")) },

                skin.Count > 0 ? StockDialogXml.Dialogs(ns, options) : null,
                Welcome(ns, options),
                hasOptions ? Options(ns, options) : null,
                Finish(ns, options),

                E("InstallUISequence",
                    E("Show", A("Dialog", WelcomeId), A("Before", D("ProgressDlg")), A("Condition", "NOT Installed")),
                    StockDialogXml.InstallShows(ns, options),
                    // Running the MSI again on an installed product offers repair, remove and (with features) change. The
                    // DialogRefs above bring ResumeDlg and MaintenanceWelcomeDlg with their own Show entries, which WiX
                    // defines as virtual: scheduling them here too is a conflict (WIX7009), so we leave them to the extension.
                    E("Show", A("Dialog", FinishId), A("OnExit", "success"))),
                E("AdminUISequence",
                    E("Show", A("Dialog", FinishId), A("OnExit", "success")),
                    StockDialogXml.AdminShows(ns, options)));

            return E("Fragment",
                options.PageBackground == null ? null : E("Binary", A("Id", PageBitmapId), A("SourceFile", options.PageBackground)),
                options.CancelBackground == null ? null : E("Binary", A("Id", CancelBitmapId), A("SourceFile", options.CancelBackground)),
                ui,
                E("UIRef", A("Id", "WixUI_Common")));
        }

        static XAttribute[] Colour(Rgb c) =>
            new[] { new XAttribute("Red", c.R), new XAttribute("Green", c.G), new XAttribute("Blue", c.B) };

        /// <summary>The install folder and the prompted values share one page after the welcome page.</summary>
        static bool HasOptionsPage(ModernDialogOptions options) => options.ChooseInstallDir || options.Prompts.Count > 0;

        static XElement Welcome(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var license = options.LicenseFile;
            // The page this one leads to, or null when its button starts the install.
            var next = HasOptionsPage(options) ? OptionsId : options.HasFeatures ? "CustomizeDlg" : null;

            var dialog = E("Dialog", A("Id", WelcomeId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns, options),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}[ProductName]"),
                Text(ns, "Subtitle", 58, 14, "{\\InstallKit_Font_Muted}Version [ProductVersion]  -  [Manufacturer]"));

            var intro = next != null
                ? "Setup will install [ProductName] on this computer. Click Next to continue."
                : "Setup will install [ProductName] on this computer. Click Install to continue.";

            if (license != null)
            {
                // The intro, then the accept box above the license text, which fills the rest of the page.
                dialog.Add(
                    Text(ns, "Intro", 82, 28, intro),
                    E("Control", A("Id", "Accept"), A("Type", "CheckBox"), A("X", ContentX), A("Y", 114), A("Width", ContentWidth), A("Height", 14),
                      A("Property", "InstallKitLicenseAccepted"), A("CheckBoxValue", "1"), A("Transparent", "yes"),
                      A("Text", "{\\InstallKit_Font_Body}I accept the license terms")),
                    E("Control", A("Id", "License"), A("Type", "ScrollableText"), A("X", ContentX), A("Y", 134), A("Width", ContentWidth),
                      A("Height", 92), A("Sunken", "yes"), A("TabSkip", "no"),
                      E("Text", A("SourceFile", license))));
            }
            else
            {
                dialog.Add(Text(ns, "Intro", 82, 40, intro));
            }

            // Without features or a page after this one the button starts the install. Otherwise it leads on, and the "Ready to
            // install" page after the feature page (which has the elevation shield and the disk space check) starts it.
            var install = E("Control", A("Id", "Install"), A("Type", "PushButton"), A("X", 236), A("Y", 243), A("Width", 56), A("Height", 17),
                A("Default", "yes"), next != null ? null : A("ElevationShield", "yes"), A("Text", next != null ? "Next" : "Install"),
                next != null
                    ? new[] { E("Publish", A("Event", "NewDialog"), A("Value", next), A("Order", 1)) }
                    : new[]
                    {
                        E("Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Order", 1), A("Condition", "OutOfDiskSpace = 1")),
                        E("Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Order", 2), A("Condition", "OutOfDiskSpace <> 1")),
                    });

            if (license != null)
            {
                // Windows Installer never undoes a condition, so the button needs the matching enable condition as well.
                install.Add(A("DisableCondition", "InstallKitLicenseAccepted <> \"1\""), A("EnableCondition", "InstallKitLicenseAccepted = \"1\""));
            }

            dialog.Add(
                options.PageBackground == null ? E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)) : null,
                install,
                E("Control", A("Id", "Cancel"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Cancel", "yes"), A("Text", "Cancel"),
                  E("Publish", A("Event", "SpawnDialog"), A("Value", StockDialogXml.Id(options, "CancelDlg")))));

            return dialog;
        }

        /// <summary>The install folder (when chosen) and the prompted values, then Back and Next (or Install).</summary>
        static XElement Options(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var features = options.HasFeatures;
            var folder = options.ChooseInstallDir;
            var prompts = options.Prompts.Count > 0;

            var dialog = E("Dialog", A("Id", OptionsId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns, options),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}" + (folder && !prompts ? "Install location" : "Settings")),
                Text(ns, "Subtitle", 58, 14, "{\\InstallKit_Font_Muted}" + (folder
                    ? "Choose where to install, and tell setup a little about this installation."
                    : "Tell setup a little about this installation.")));

            var y = 82;
            if (folder)
            {
                dialog.Add(
                    Text(ns, "FolderLabel", y, 12, "Install to:"),
                    E("Control", A("Id", "Folder"), A("Type", "PathEdit"), A("X", ContentX), A("Y", y + 14), A("Width", ContentWidth - 62), A("Height", 17),
                      A("Property", "INSTALLDIR"), A("Indirect", "no")),
                    E("Control", A("Id", "Browse"), A("Type", "PushButton"), A("X", ContentX + ContentWidth - 56), A("Y", y + 14), A("Width", 56), A("Height", 17),
                      A("Text", "Browse..."),
                      E("Publish", A("Property", "_BrowseProperty"), A("Value", "INSTALLDIR"), A("Order", 1)),
                      E("Publish", A("Event", "SpawnDialog"), A("Value", "BrowseDlg"), A("Order", 2))));
                y += 40;
            }

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
            // The chosen folder has to be applied before the page is left.
            var order = 1;
            var forward = E("Control", A("Id", "Next"), A("Type", "PushButton"), A("X", 236), A("Y", 243), A("Width", 56), A("Height", 17),
                A("Default", "yes"), features ? null : A("ElevationShield", "yes"), A("Text", features ? "Next" : "Install"),
                folder ? E("Publish", A("Event", "SetTargetPath"), A("Value", "INSTALLDIR"), A("Order", order++)) : null,
                features
                    ? new[] { E("Publish", A("Event", "NewDialog"), A("Value", "CustomizeDlg"), A("Order", order)) }
                    : new[]
                    {
                        E("Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Order", order), A("Condition", "OutOfDiskSpace = 1")),
                        E("Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Order", order + 1), A("Condition", "OutOfDiskSpace <> 1")),
                    });
            if (required.Count > 0)
            {
                // Windows Installer only applies a condition while it is true: it never undoes it. Without the matching
                // EnableCondition the button stays disabled for good once a required box has been emptied, even after the user
                // types a value again.
                var filled = options.Prompts.Where(p => p.Required).Select(p => $"{p.Property} <> \"\"");
                forward.Add(A("DisableCondition", string.Join(" OR ", required)), A("EnableCondition", string.Join(" AND ", filled)));
            }

            dialog.Add(
                options.PageBackground == null ? E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)) : null,
                E("Control", A("Id", "Back"), A("Type", "PushButton"), A("X", 180), A("Y", 243), A("Width", 56), A("Height", 17), A("Text", "Back"),
                  E("Publish", A("Event", "NewDialog"), A("Value", WelcomeId))),
                forward,
                E("Control", A("Id", "Cancel"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Cancel", "yes"), A("Text", "Cancel"),
                  E("Publish", A("Event", "SpawnDialog"), A("Value", StockDialogXml.Id(options, "CancelDlg")))));

            return dialog;
        }

        static XElement Finish(XNamespace ns, ModernDialogOptions options)
        {
            XElement E(string name, params object?[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            return E("Dialog", A("Id", FinishId), A("Width", Width), A("Height", Height), A("Title", "[ProductName] Setup"),
                Bitmap(ns, options),
                Text(ns, "Title", 20, 36, "{\\InstallKit_Font_Hero}All set"),
                Text(ns, "Description", 62, 40, "Setup finished. Click Finish to close this window."),
                options.PageBackground == null ? E("Control", A("Id", "BottomLine"), A("Type", "Line"), A("X", 0), A("Y", 234), A("Width", Width), A("Height", 0)) : null,
                E("Control", A("Id", "Finish"), A("Type", "PushButton"), A("X", 304), A("Y", 243), A("Width", 56), A("Height", 17),
                  A("Default", "yes"), A("Cancel", "yes"), A("Text", "Finish"),
                  E("Publish", A("Event", "EndDialog"), A("Value", "Return"))));
        }

        internal static XElement Bitmap(XNamespace ns, ModernDialogOptions options) =>
            new XElement(ns + "Control",
                new XAttribute("Id", "Background"), new XAttribute("Type", "Bitmap"),
                new XAttribute("X", 0), new XAttribute("Y", 0), new XAttribute("Width", Width),
                // The page background reaches under the buttons; the stock bitmap stops above them.
                new XAttribute("Height", options.PageBackground == null ? 234 : Height),
                new XAttribute("TabSkip", "no"), new XAttribute("Text", options.PageBackground == null ? "WixUI_Bmp_Dialog" : PageBitmapId));

        internal static XElement Text(XNamespace ns, string id, int y, int height, string text, string? showCondition = null) =>
            new XElement(ns + "Control",
                new XAttribute("Id", id), new XAttribute("Type", "Text"),
                new XAttribute("X", ContentX), new XAttribute("Y", y), new XAttribute("Width", ContentWidth), new XAttribute("Height", height),
                new XAttribute("Transparent", "yes"), new XAttribute("NoPrefix", "yes"),
                showCondition == null ? null : new XAttribute("Hidden", "yes"),
                showCondition == null ? null : new XAttribute("ShowCondition", showCondition),
                new XAttribute("Text", text.StartsWith("{\\") ? text : "{\\InstallKit_Font_Body}" + text));

        internal static XElement Publish(XNamespace ns, string dialog, string control, string @event, string value, int? order = null, string? condition = null) =>
            new XElement(ns + "Publish",
                new XAttribute("Dialog", dialog), new XAttribute("Control", control),
                new XAttribute("Event", @event), new XAttribute("Value", value),
                order == null ? null : new XAttribute("Order", order),
                condition == null ? null : new XAttribute("Condition", condition));
    }
}
