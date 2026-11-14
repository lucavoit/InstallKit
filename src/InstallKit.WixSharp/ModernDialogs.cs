using System.IO;
using System.Linq;
using System.Xml.Linq;
using WixSharp;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// The native modern dialog set (<see cref="InstallerUi.Modern"/>): built from MSI's own dialog tables, so it
    /// needs nothing on the target machine and works the same on every engine.
    /// <list type="bullet">
    /// <item>One welcome page on the branded background: product name, version, optional license with an
    /// accept box, optional install folder with Browse, and an Install button.</item>
    /// <item>A finish page on the same background.</item>
    /// <item>Progress, errors, files-in-use and repair/remove reuse the WiX UI extension's dialogs, which pick
    /// up the generated banner and the Segoe UI text styles defined here.</item>
    /// </list>
    /// Implemented as a WiX UI fragment injected into the generated source in place of WixUI_Minimal.
    /// </summary>
    internal static class ModernDialogs
    {
        public const string UiId = "InstallKit_Modern";
        const string WelcomeId = "InstallKit_WelcomeDlg";
        const string FinishId = "InstallKit_FinishDlg";

        // Dialog units. 370x270 is the standard WiX dialog size; the background bitmap covers 370x234.
        const int Width = 370;
        const int Height = 270;
        const int ContentX = 135; // right of the 164 px accent panel in the generated background
        const int ContentWidth = 220;

        public static void Apply(Project project, MsiDefinition msi)
        {
            var license = string.IsNullOrWhiteSpace(msi.LicenseFile) ? null : Path.GetFullPath(msi.LicenseFile);
            var chooseFolder = msi.Ui == InstallerUi.ModernInstallDir;

            project.WixSourceGenerated += document =>
            {
                var root = document.Root!;
                XNamespace ns = root.Name.Namespace;

                // WixSharp references WixUI_Minimal (which also pulls in the UI extension); swap in ours.
                foreach (var uiRef in root.Descendants(ns + "UIRef").Where(e => (string?)e.Attribute("Id") == "WixUI_Minimal").ToList())
                    uiRef.SetAttributeValue("Id", UiId);

                root.Add(BuildFragment(ns, license, chooseFolder));
            };
        }

        static XElement BuildFragment(XNamespace ns, string? license, bool chooseFolder)
        {
            XElement E(string name, params object[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

            var ui = E("UI", A("Id", UiId),
                // Segoe UI everywhere, including the reused WiX dialogs.
                E("TextStyle", A("Id", "WixUI_Font_Normal"), A("FaceName", "Segoe UI"), A("Size", 9)),
                E("TextStyle", A("Id", "WixUI_Font_Bigger"), A("FaceName", "Segoe UI"), A("Size", 12)),
                E("TextStyle", A("Id", "WixUI_Font_Title"), A("FaceName", "Segoe UI Semibold"), A("Size", 9)),
                E("TextStyle", A("Id", "InstallKit_Font_Hero"), A("FaceName", "Segoe UI Light"), A("Size", 20)),
                E("TextStyle", A("Id", "InstallKit_Font_Muted"), A("FaceName", "Segoe UI"), A("Size", 9), A("Red", 96), A("Green", 96), A("Blue", 96)),
                E("Property", A("Id", "DefaultUIFont"), A("Value", "WixUI_Font_Normal")),
                E("Property", A("Id", "WixUI_Mode"), A("Value", "Minimal")),
                E("Property", A("Id", "ARPNOMODIFY"), A("Value", "1")),

                // Standard dialogs we reuse as-is.
                new[]
                {
                    "ErrorDlg", "FatalError", "FilesInUse", "MsiRMFilesInUse", "PrepareDlg", "ProgressDlg", "ResumeDlg",
                    "UserExit", "CancelDlg", "OutOfDiskDlg", "MaintenanceWelcomeDlg", "MaintenanceTypeDlg", "VerifyReadyDlg",
                }
                .Concat(chooseFolder ? new[] { "BrowseDlg" } : new string[0])
                .Select(id => E("DialogRef", A("Id", id))),

                // Repair / remove flow (as in WixUI_Minimal).
                Publish(ns, "MaintenanceWelcomeDlg", "Next", "NewDialog", "MaintenanceTypeDlg"),
                Publish(ns, "MaintenanceTypeDlg", "RepairButton", "NewDialog", "VerifyReadyDlg"),
                Publish(ns, "MaintenanceTypeDlg", "RemoveButton", "NewDialog", "VerifyReadyDlg"),
                Publish(ns, "MaintenanceTypeDlg", "Back", "NewDialog", "MaintenanceWelcomeDlg"),
                Publish(ns, "VerifyReadyDlg", "Back", "NewDialog", "MaintenanceTypeDlg"),

                Welcome(ns, license, chooseFolder),
                Finish(ns),

                E("InstallUISequence",
                    E("Show", A("Dialog", WelcomeId), A("Before", "ProgressDlg"), A("Condition", "NOT Installed")),
                    E("Show", A("Dialog", FinishId), A("OnExit", "success"))),
                E("AdminUISequence",
                    E("Show", A("Dialog", FinishId), A("OnExit", "success"))));

            return E("Fragment", ui, E("UIRef", A("Id", "WixUI_Common")));
        }

        static XElement Welcome(XNamespace ns, string? license, bool chooseFolder)
        {
            XElement E(string name, params object[] content) => new XElement(ns + name, content);
            XAttribute A(string name, object value) => new XAttribute(name, value);

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
                dialog.Add(Text(ns, "Intro", y, 40, "Setup will install [ProductName] on this computer. Click Install to continue."));
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

            var install = E("Control", A("Id", "Install"), A("Type", "PushButton"), A("X", 236), A("Y", 243), A("Width", 56), A("Height", 17),
                A("Default", "yes"), A("ElevationShield", "yes"), A("Text", "Install"),
                chooseFolder ? E("Publish", A("Event", "SetTargetPath"), A("Value", "INSTALLDIR"), A("Order", 1)) : null,
                E("Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Order", 2), A("Condition", "OutOfDiskSpace = 1")),
                E("Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Order", 3), A("Condition", "OutOfDiskSpace <> 1")));

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

        static XElement Publish(XNamespace ns, string dialog, string control, string @event, string value) =>
            new XElement(ns + "Publish",
                new XAttribute("Dialog", dialog), new XAttribute("Control", control),
                new XAttribute("Event", @event), new XAttribute("Value", value));
    }
}
