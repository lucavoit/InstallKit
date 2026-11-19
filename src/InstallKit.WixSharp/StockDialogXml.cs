using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Our own versions of the WiX UI extension's dialogs that every install, repair and remove goes through, on the
    /// same page background as the modern pages: progress, "ready to install", the repair/remove pages, resume,
    /// the interrupted and failed pages and the cancel prompt. Same dialog and control ids, events and conditions as
    /// WiX's, so the flows that lead to them work unchanged; only the look differs.
    /// <para>
    /// Not restyled: the error popup (Windows Installer replaces its text, which drops our text style), the browse,
    /// feature and files-in-use pages and the disk space popups. They keep WiX's look.
    /// </para>
    /// Plain XML, no WixSharp types, so it can be unit tested.
    /// </summary>
    internal static class StockDialogXml
    {
        const string Hero = "{\\InstallKit_Font_Hero}";
        const string Muted = "{\\InstallKit_Font_Muted}";

        static readonly string[] PageDialogs =
        {
            "ProgressDlg", "MaintenanceWelcomeDlg", "MaintenanceTypeDlg", "VerifyReadyDlg", "ResumeDlg", "UserExit", "FatalError",
        };

        /// <summary>The ids of the WiX dialogs this class replaces for these options.</summary>
        public static ISet<string> Skins(ModernDialogOptions options)
        {
            var ids = new HashSet<string>();
            if (options.PageBackground != null)
                ids.UnionWith(PageDialogs);
            if (options.CancelBackground != null)
                ids.Add("CancelDlg");
            return ids;
        }

        public static IEnumerable<XElement> Dialogs(XNamespace ns, ModernDialogOptions options)
        {
            var skin = Skins(options);
            var all = new List<XElement>();
            if (skin.Contains("ProgressDlg"))
            {
                // Declared next to the repair page by WiX's own file; they tell it which Burn-driven action is allowed.
                all.AddRange(new[] { "BURNMSIMODIFY", "BURNMSIREPAIR", "BURNMSIUNINSTALL" }.Select(id => E(ns, "Property", A("Id", id), A("Secure", "yes"))));
                all.AddRange(new[]
                {
                    Progress(ns, options), MaintenanceWelcome(ns, options), MaintenanceType(ns, options), VerifyReady(ns, options),
                    Resume(ns, options), Exit(ns, options, "UserExit", "Setup stopped", "!(loc.UserExitDescription1) !(loc.UserExitDescription2)"),
                    Exit(ns, options, "FatalError", "Setup failed", "!(loc.FatalErrorDescription1) !(loc.FatalErrorDescription2)"),
                });
            }

            if (skin.Contains("CancelDlg"))
                all.Add(Cancel(ns, options));

            return all;
        }

        /// <summary>The Show entries of the install UI sequence that WiX's dialogs would have brought.</summary>
        public static IEnumerable<XElement> InstallShows(XNamespace ns, ISet<string> skin)
        {
            if (!skin.Contains("ProgressDlg"))
                yield break;

            yield return Show(ns, "ProgressDlg", "Before", "ExecuteAction");
            yield return Show(ns, "MaintenanceWelcomeDlg", "Before", "ProgressDlg", "Installed AND NOT RESUME AND NOT Preselected AND NOT PATCH");
            yield return Show(ns, "ResumeDlg", "Before", "ProgressDlg", "Installed AND (RESUME OR Preselected)");
            yield return Show(ns, "UserExit", "OnExit", "cancel");
            yield return Show(ns, "FatalError", "OnExit", "error");
        }

        public static IEnumerable<XElement> AdminShows(XNamespace ns, ISet<string> skin)
        {
            if (!skin.Contains("ProgressDlg"))
                yield break;

            yield return Show(ns, "UserExit", "OnExit", "cancel");
            yield return Show(ns, "FatalError", "OnExit", "error");
        }

        static XElement Show(XNamespace ns, string dialog, string placement, string anchor, string? condition = null) =>
            new XElement(ns + "Show", new XAttribute("Dialog", dialog), new XAttribute(placement, anchor),
                condition == null ? null : new XAttribute("Condition", condition));

        static XElement E(XNamespace ns, string name, params object?[] content) => new XElement(ns + name, content);

        static XAttribute A(string name, object value) => new XAttribute(name, value);

        static XElement Page(XNamespace ns, ModernDialogOptions options, string id, string title, params object?[] content) =>
            E(ns, "Dialog", A("Id", id), A("Width", ModernDialogXml.Width), A("Height", ModernDialogXml.Height), A("Title", "[ProductName] Setup"),
                ModernDialogXml.Bitmap(ns, options),
                ModernDialogXml.Text(ns, "Title", 20, 36, Hero + title),
                content);

        static XElement Button(XNamespace ns, string id, int x, int width, string text, bool isDefault = false, bool isCancel = false,
            bool disabled = false, int y = 243, object? content = null) =>
            E(ns, "Control", A("Id", id), A("Type", "PushButton"), A("X", x), A("Y", y), A("Width", width), A("Height", 17),
                isDefault ? A("Default", "yes") : null, isCancel ? A("Cancel", "yes") : null, disabled ? A("Disabled", "yes") : null,
                A("Text", text), content);

        static XElement Back(XNamespace ns, int x = 180, bool disabled = true, object? content = null) =>
            Button(ns, "Back", x, 56, "!(loc.WixUIBack)", disabled: disabled, content: content);

        static XElement Progress(XNamespace ns, ModernDialogOptions options)
        {
            var modes = new[]
            {
                ("Installing", "NOT Installed OR (Installed AND (RESUME OR Preselected) AND NOT PATCH)", "!(loc.ProgressDlgTextInstalling)"),
                ("Changing", "WixUI_InstallMode = \"Change\"", "!(loc.ProgressDlgTextChanging)"),
                ("Repairing", "WixUI_InstallMode = \"Repair\"", "!(loc.ProgressDlgTextRepairing)"),
                ("Removing", "WixUI_InstallMode = \"Remove\"", "!(loc.ProgressDlgTextRemoving)"),
                ("Updating", "WixUI_InstallMode = \"Update\"", "!(loc.ProgressDlgTextUpdating)"),
            };

            var dialog = E(ns, "Dialog", A("Id", "ProgressDlg"), A("Width", ModernDialogXml.Width), A("Height", ModernDialogXml.Height),
                A("Title", "[ProductName] Setup"), A("Modeless", "yes"),
                ModernDialogXml.Bitmap(ns, options));

            // The title and text of the mode that applies are shown; the others stay hidden.
            foreach (var (name, condition, text) in modes)
            {
                dialog.Add(
                    ModernDialogXml.Text(ns, "Title" + name, 20, 36, Hero + name, condition),
                    ModernDialogXml.Text(ns, "Text" + name, 62, 36, text, condition));
            }

            dialog.Add(
                E(ns, "Control", A("Id", "ProgressBar"), A("Type", "ProgressBar"), A("X", ModernDialogXml.ContentX), A("Y", 118),
                  A("Width", ModernDialogXml.ContentWidth), A("Height", 8), A("ProgressBlocks", "no"), A("Text", "!(loc.ProgressDlgProgressBar)"),
                  E(ns, "Subscribe", A("Event", "SetProgress"), A("Attribute", "Progress"))),
                Back(ns, 180),
                Button(ns, "Next", 236, 56, "!(loc.WixUINext)", disabled: true),
                Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", isDefault: true, isCancel: true,
                    content: E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));
            return dialog;
        }

        static XElement MaintenanceWelcome(XNamespace ns, ModernDialogOptions options) =>
            E(ns, "Dialog", A("Id", "MaintenanceWelcomeDlg"), A("Width", ModernDialogXml.Width), A("Height", ModernDialogXml.Height), A("Title", "[ProductName] Setup"),
                ModernDialogXml.Bitmap(ns, options),
                ModernDialogXml.Text(ns, "Title", 20, 36, Hero + "[ProductName]"),
                ModernDialogXml.Text(ns, "Subtitle", 58, 14, Muted + "Version [ProductVersion]  -  [Manufacturer]"),
                ModernDialogXml.Text(ns, "Description", 82, 60, "!(loc.MaintenanceWelcomeDlgDescription)"),
                Back(ns),
                Button(ns, "Next", 236, 56, "!(loc.WixUINext)", isDefault: true,
                    content: E(ns, "Publish", A("Event", "SpawnWaitDialog"), A("Value", "WaitForCostingDlg"), A("Condition", "!(wix.WixUICostingPopupOptOut) OR CostingComplete = 1"))),
                Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", isCancel: true, content: E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));

        static XElement MaintenanceType(XNamespace ns, ModernDialogOptions options)
        {
            var dialog = Page(ns, options, "MaintenanceTypeDlg", "Maintenance");

            void AddAction(string key, string button, string textKey, int y, string disabledWhen, string defaultAttr)
            {
                dialog.Add(
                    E(ns, "Control", A("Id", button), A("Type", "PushButton"), A("X", ModernDialogXml.ContentX), A("Y", y), A("Width", 80), A("Height", 17),
                      defaultAttr == "" ? null : A("Default", "yes"), A("ToolTip", $"!(loc.MaintenanceTypeDlg{key}ButtonTooltip)"),
                      A("Text", $"!(loc.MaintenanceTypeDlg{key}Button)"), A("DisableCondition", disabledWhen),
                      E(ns, "Publish", A("Property", "WixUI_InstallMode"), A("Value", key))),
                    ModernDialogXml.Text(ns, textKey + "Text", y + 20, 24, $"!(loc.MaintenanceTypeDlg{key}Text)").WithHide(disabledWhen),
                    ModernDialogXml.Text(ns, textKey + "DisabledText", y + 20, 24, $"!(loc.MaintenanceTypeDlg{key}DisabledText)", disabledWhen));
            }

            AddAction("Change", "ChangeButton", "Change", 66, "ARPNOMODIFY OR BURNMSIREPAIR OR BURNMSIUNINSTALL", "default");
            AddAction("Repair", "RepairButton", "Repair", 118, "ARPNOREPAIR OR BURNMSIMODIFY OR BURNMSIUNINSTALL", "");
            AddAction("Remove", "RemoveButton", "Remove", 170, "ARPNOREMOVE OR BURNMSIREPAIR OR BURNMSIMODIFY", "");

            dialog.Add(
                Back(ns, disabled: false),
                Button(ns, "Next", 236, 56, "!(loc.WixUINext)", disabled: true),
                Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", isCancel: true, content: E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));
            return dialog;
        }

        static XElement WithHide(this XElement control, string condition)
        {
            control.Add(new XAttribute("HideCondition", condition));
            return control;
        }

        static XElement VerifyReady(XNamespace ns, ModernDialogOptions options)
        {
            const string diskSpace = "OutOfDiskSpace <> 1";
            const string rollbackPrompt = "OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 0 AND (PROMPTROLLBACKCOST=\"P\" OR NOT PROMPTROLLBACKCOST)";
            const string rollbackDiscard = "OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 0 AND PROMPTROLLBACKCOST=\"D\"";
            const string noRoom = "(OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 1) OR (OutOfDiskSpace = 1 AND PROMPTROLLBACKCOST=\"F\")";

            // The disk space handling every action button shares. The first events are what the button does.
            IEnumerable<XElement> Events(params XElement[] own) => own.Concat(new[]
            {
                E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "OutOfRbDiskDlg"), A("Condition", rollbackPrompt)),
                E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Condition", rollbackDiscard)),
                E(ns, "Publish", A("Event", "EnableRollback"), A("Value", "False"), A("Condition", rollbackDiscard)),
                E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Condition", noRoom)),
            });

            XElement End(string value = "Return") => E(ns, "Publish", A("Event", "EndDialog"), A("Value", value), A("Condition", diskSpace));
            XElement Do(string @event, string value) => E(ns, "Publish", A("Event", @event), A("Value", value), A("Condition", diskSpace));

            // (id, shield, label, show condition, enable condition, makes this the default button, what it does)
            var actions = new (string Id, bool? Shield, string Text, string Show, string Enable, bool Default, XElement[] Own)[]
            {
                ("Install", true, "VerifyReadyDlgInstall", "NOT Installed AND ALLUSERS", "NOT Installed", true, new[] { End() }),
                ("InstallNoShield", false, "VerifyReadyDlgInstall", "NOT Installed AND NOT ALLUSERS", "NOT Installed", true, new[] { End() }),
                ("Change", true, "VerifyReadyDlgChange", "WixUI_InstallMode = \"Change\" AND ALLUSERS AND (ADDLOCAL OR REMOVE)", "WixUI_InstallMode = \"Change\"", true, new[] { End() }),
                ("ChangeNoShield", false, "VerifyReadyDlgChange", "WixUI_InstallMode = \"Change\" AND (NOT ALLUSERS OR (NOT ADDLOCAL AND NOT REMOVE))", "WixUI_InstallMode = \"Change\"", true, new[] { End() }),
                ("Repair", null, "VerifyReadyDlgRepair", "WixUI_InstallMode = \"Repair\"", "WixUI_InstallMode = \"Repair\"", true, new[] { Do("ReinstallMode", "ecmus"), Do("Reinstall", "All"), End() }),
                ("Remove", true, "VerifyReadyDlgRemove", "WixUI_InstallMode = \"Remove\" AND ALLUSERS", "WixUI_InstallMode = \"Remove\"", false, new[] { Do("Remove", "All"), End() }),
                ("RemoveNoShield", false, "VerifyReadyDlgRemove", "WixUI_InstallMode = \"Remove\" AND NOT ALLUSERS", "WixUI_InstallMode = \"Remove\"", false, new[] { Do("Remove", "All"), End() }),
                ("Update", true, "VerifyReadyDlgUpdate", "WixUI_InstallMode = \"Update\" AND ALLUSERS", "WixUI_InstallMode = \"Update\"", false, new[] { End() }),
                ("UpdateNoShield", false, "VerifyReadyDlgUpdate", "WixUI_InstallMode = \"Update\" AND NOT ALLUSERS", "WixUI_InstallMode = \"Update\"", false, new[] { End() }),
            };

            var dialog = E(ns, "Dialog", A("Id", "VerifyReadyDlg"), A("Width", ModernDialogXml.Width), A("Height", ModernDialogXml.Height),
                A("Title", "[ProductName] Setup"), A("TrackDiskSpace", "yes"), ModernDialogXml.Bitmap(ns, options));

            foreach (var action in actions)
            {
                var button = Button(ns, action.Id, 212, 80, "!(loc." + action.Text + ")", isDefault: action.Default, disabled: true,
                    content: Events(action.Own).Cast<object?>().ToArray());
                // Hidden until its condition says so; the stock buttons do the same.
                button.Add(A("Hidden", "yes"), A("ShowCondition", action.Show), A("EnableCondition", action.Enable));
                if (action.Shield != null)
                    button.Add(A("ElevationShield", action.Shield.Value ? "yes" : "no"));
                if (action.Default)
                    button.Add(A("DefaultCondition", action.Enable));
                dialog.Add(button);
            }

            var modes = new[]
            {
                ("Install", "Ready to install", "NOT Installed"),
                ("Change", "Ready to change", "WixUI_InstallMode = \"Change\""),
                ("Repair", "Ready to repair", "WixUI_InstallMode = \"Repair\""),
                ("Remove", "Ready to remove", "WixUI_InstallMode = \"Remove\""),
                ("Update", "Ready to update", "WixUI_InstallMode = \"Update\""),
            };
            foreach (var (name, title, condition) in modes)
            {
                dialog.Add(
                    ModernDialogXml.Text(ns, name + "Title", 20, 36, Hero + title, condition),
                    ModernDialogXml.Text(ns, name + "Text", 66, 110, $"!(loc.VerifyReadyDlg{name}Text)", condition));
            }

            // Back is the default in remove mode, where the Remove button deliberately is not.
            var back = Back(ns, 156, disabled: false);
            back.Add(A("DefaultCondition", "WixUI_InstallMode = \"Remove\""));
            dialog.Add(
                back,
                Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", isCancel: true, content: E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));
            return dialog;
        }

        static XElement Resume(XNamespace ns, ModernDialogOptions options)
        {
            var dialog = Page(ns, options, "ResumeDlg", "Resume setup",
                ModernDialogXml.Text(ns, "Description", 66, 60, "!(loc.ResumeDlgDescription)"),
                Back(ns, 156));

            foreach (var (id, shield, show) in new[] { ("Install", "yes", "ALLUSERS"), ("InstallNoShield", "no", "NOT ALLUSERS") })
            {
                dialog.Add(Button(ns, id, 212, 80, "!(loc.ResumeDlgInstall)", isDefault: true,
                    content: new object?[]
                    {
                        A("ElevationShield", shield), A("Hidden", "yes"), A("ShowCondition", show),
                        E(ns, "Publish", A("Event", "SpawnWaitDialog"), A("Value", "WaitForCostingDlg"), A("Condition", "!(wix.WixUICostingPopupOptOut) OR CostingComplete = 1")),
                        E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Condition", "OutOfDiskSpace <> 1")),
                        E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "OutOfRbDiskDlg"), A("Condition", "OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 0 AND (PROMPTROLLBACKCOST=\"P\" OR NOT PROMPTROLLBACKCOST)")),
                        E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Return"), A("Condition", "OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 0 AND PROMPTROLLBACKCOST=\"D\"")),
                        E(ns, "Publish", A("Event", "EnableRollback"), A("Value", "False"), A("Condition", "OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 0 AND PROMPTROLLBACKCOST=\"D\"")),
                        E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "OutOfDiskDlg"), A("Condition", "(OutOfDiskSpace = 1 AND OutOfNoRbDiskSpace = 1) OR (OutOfDiskSpace = 1 AND PROMPTROLLBACKCOST=\"F\")")),
                    }));
            }

            dialog.Add(Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", isCancel: true, content: E(ns, "Publish", A("Event", "SpawnDialog"), A("Value", "CancelDlg"))));
            return dialog;
        }

        /// <summary>The "interrupted" and "failed" pages: a heading, WiX's explanation and a Finish button.</summary>
        static XElement Exit(XNamespace ns, ModernDialogOptions options, string id, string title, string description) =>
            Page(ns, options, id, title,
                ModernDialogXml.Text(ns, "Description", 66, 100, description),
                Back(ns),
                Button(ns, "Finish", 236, 56, "!(loc.WixUIFinish)", isDefault: true, isCancel: true,
                    content: E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Exit"))),
                Button(ns, "Cancel", 304, 56, "!(loc.WixUICancel)", disabled: true));

        /// <summary>The cancel prompt: a small dialog on its own background, with the question and Yes / No.</summary>
        static XElement Cancel(XNamespace ns, ModernDialogOptions options) =>
            E(ns, "Dialog", A("Id", "CancelDlg"), A("Width", 260), A("Height", 85), A("Title", "[ProductName] Setup"),
                E(ns, "Control", A("Id", "Background"), A("Type", "Bitmap"), A("X", 0), A("Y", 0), A("Width", 260), A("Height", 85),
                  A("TabSkip", "no"), A("Text", ModernDialogXml.CancelBitmapId)),
                E(ns, "Control", A("Id", "Icon"), A("Type", "Icon"), A("X", 15), A("Y", 15), A("Width", 24), A("Height", 24),
                  A("ToolTip", "!(loc.CancelDlgIconTooltip)"), A("FixedSize", "yes"), A("IconSize", 32), A("Text", "!(loc.CancelDlgIcon)")),
                E(ns, "Control", A("Id", "Text"), A("Type", "Text"), A("X", 48), A("Y", 15), A("Width", 194), A("Height", 30),
                  A("Transparent", "yes"), A("NoPrefix", "yes"), A("Text", "{\\InstallKit_Font_Body}!(loc.CancelDlgText)")),
                Button(ns, "No", 132, 56, "!(loc.WixUINo)", isDefault: true, isCancel: true, y: 57, content: E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Return"))),
                Button(ns, "Yes", 72, 56, "!(loc.WixUIYes)", y: 57, content: E(ns, "Publish", A("Event", "EndDialog"), A("Value", "Exit"))));
    }
}
