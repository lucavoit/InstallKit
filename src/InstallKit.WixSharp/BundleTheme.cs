using System;
using System.Linq;
using System.Xml.Linq;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Writes the theme of the setup window of a bundle (the WiX standard bootstrapper application): WiX's own layout
    /// with the user's colours and a slightly roomier window. Only fonts, sizes and colours differ from the stock
    /// theme; control names and pages are unchanged, because the application looks them up by name.
    /// Plain XML, no WixSharp types, so it can be unit tested.
    /// </summary>
    internal static class BundleTheme
    {
        /// <summary>How the bundle shows its license; decides which controls of the Install page stay.</summary>
        public enum LicenseStyle
        {
            /// <summary>No license.</summary>
            None,

            /// <summary>A link to a web page.</summary>
            Link,

            /// <summary>An .rtf shown in the window.</summary>
            Rtf,
        }

        static readonly XNamespace Ns = "http://wixtoolset.org/schemas/v4/thmutil";

        public static XDocument Build(Palette palette, LicenseStyle license)
        {
            var theme = XElement.Parse(StockRtfTheme);

            // Fonts: 0 buttons and default, 1 title, 2 page headings (accent), 3 body, 4 the license (paper), 5 input boxes.
            // Every font carries the background, so each control paints onto the window colour.
            theme.Elements(Ns + "Font").Remove();
            var background = palette.Background.Hex;
            theme.AddFirst(
                Font(0, -12, 400, palette.Text, background),
                Font(1, -28, 600, palette.Text, background),
                Font(2, -20, 600, palette.Heading, background),
                Font(3, -13, 400, palette.Text, background),
                // The license is rich text with its own colours, so it stays dark on white in every theme.
                Font(4, -12, 400, new Rgb(24, 24, 27), "FFFFFF"),
                Font(5, -13, 400, palette.Text, palette.Surface.Hex));

            var window = theme.Element(Ns + "Window")!;
            window.SetAttributeValue("Width", "540");
            window.SetAttributeValue("Height", "360");

            // A bit more air around the logo and title.
            foreach (var image in window.Elements(Ns + "ImageControl"))
            {
                image.SetAttributeValue("X", "16");
                image.SetAttributeValue("Y", "14");
                image.SetAttributeValue("Width", "56");
                image.SetAttributeValue("Height", "56");
            }

            foreach (var title in window.Elements(Ns + "Label").Where(l => (string?)l.Attribute("FontId") == "1"))
            {
                title.SetAttributeValue("X", "84");
                title.SetAttributeValue("Y", "14");
                title.SetAttributeValue("Height", "56");
            }

            foreach (var editor in theme.Descendants(Ns + "Editbox"))
                editor.SetAttributeValue("FontId", "5");

            var install = window.Elements(Ns + "Page").Single(p => (string?)p.Attribute("Name") == "Install");
            var richedit = install.Element(Ns + "Richedit")!;
            switch (license)
            {
                case LicenseStyle.Rtf:
                    richedit.SetAttributeValue("FontId", "4");
                    break;

                case LicenseStyle.Link:
                    richedit.ReplaceWith(new XElement(Ns + "Hypertext",
                        new XAttribute("Name", "EulaHyperlink"), new XAttribute("X", 11), new XAttribute("Y", 121), new XAttribute("Width", -11),
                        new XAttribute("Height", 51), new XAttribute("TabStop", "yes"), new XAttribute("FontId", 3), new XAttribute("HideWhenDisabled", "yes"),
                        "#(loc.InstallLicenseLinkText)"));
                    break;

                default:
                    richedit.Remove();
                    install.Elements(Ns + "Checkbox").Where(c => (string?)c.Attribute("Name") == "EulaAcceptCheckbox").Remove();
                    break;
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), theme);
        }

        static XElement Font(int id, int height, int weight, Rgb foreground, string background) =>
            Font(id, height, weight, foreground.Hex, background);

        static XElement Font(int id, int height, int weight, string foreground, string background) =>
            new XElement(Ns + "Font", new XAttribute("Id", id), new XAttribute("Height", height), new XAttribute("Weight", weight),
                new XAttribute("Foreground", foreground), new XAttribute("Background", background), "Segoe UI");

        // WiX's RtfTheme.xml (rtfLicense), the layout every variant starts from.
        const string StockRtfTheme = @"<Theme xmlns=""http://wixtoolset.org/schemas/v4/thmutil"">
    <Font Id=""0"" Height=""-12"" Weight=""500"" Foreground=""windowtext"" Background=""window"">Segoe UI</Font>
    <Font Id=""1"" Height=""-24"" Weight=""500"" Foreground=""windowtext"">Segoe UI</Font>
    <Font Id=""2"" Height=""-22"" Weight=""500"" Foreground=""graytext"">Segoe UI</Font>
    <Font Id=""3"" Height=""-12"" Weight=""500"" Foreground=""windowtext"" Background=""window"">Segoe UI</Font>

    <Window Width=""485"" Height=""300"" HexStyle=""100a0000"" FontId=""0"" Caption=""#(loc.Caption)"">
        <ImageControl X=""11"" Y=""11"" Width=""64"" Height=""64"" ImageFile=""logo.png"" Visible=""yes""/>
        <Label X=""80"" Y=""11"" Width=""-11"" Height=""64"" FontId=""1"" Visible=""yes"" DisablePrefix=""yes"">#(loc.Title)</Label>

        <Page Name=""Help"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">#(loc.HelpHeader)</Label>
            <Label X=""11"" Y=""112"" Width=""-11"" Height=""-35"" FontId=""3"" DisablePrefix=""yes"">#(loc.HelpText)</Label>
            <Button Name=""HelpCloseButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.HelpCloseButton)</Text>
                <CloseWindowAction />
            </Button>
        </Page>
        <Page Name=""Loading"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"" Visible=""no"" Name=""CheckingForUpdatesLabel"" />
        </Page>
        <Page Name=""Install"">
            <Richedit Name=""EulaRichedit"" X=""11"" Y=""80"" Width=""-11"" Height=""-70"" TabStop=""yes"" FontId=""0"" HexStyle=""800000"" />
            <Checkbox Name=""EulaAcceptCheckbox"" X=""-11"" Y=""-41"" Width=""260"" Height=""17"" TabStop=""yes"" FontId=""3"" HideWhenDisabled=""yes"">#(loc.InstallAcceptCheckbox)</Checkbox>
            <Button Name=""InstallUpdateButton"" X=""11"" Y=""-11"" Width=""200"" Height=""23"" TabStop=""yes"" FontId=""0"" EnableCondition=""WixStdBAUpdateAvailable"" HideWhenDisabled=""yes"">#(loc.UpdateButton)</Button>
            <Button Name=""OptionsButton"" X=""-171"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" VisibleCondition=""NOT WixStdBASuppressOptionsUI"">
                <Text>#(loc.InstallOptionsButton)</Text>
                <ChangePageAction Page=""Options"" />
            </Button>
            <Button Name=""InstallButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" ElevationShieldCondition='(WixBundleAuthoredScope = 1 AND NOT WixBundleElevated) OR ((WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3) AND WixStdBAScope = ""PerMachine"")'>#(loc.InstallInstallButton)</Button>
            <Button Name=""InstallCancelButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.InstallCancelButton)</Text>
                <CloseWindowAction />
            </Button>
        </Page>
        <Page Name=""Options"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">#(loc.OptionsHeader)</Label>
            <Label X=""11"" Y=""121"" Width=""-11"" Height=""17"" FontId=""3"" DisablePrefix=""yes"">#(loc.OptionsLocationLabel)</Label>
            <Editbox Name=""InstallFolder"" X=""11"" Y=""143"" Width=""-91"" Height=""21"" TabStop=""yes"" FontId=""3"" FileSystemAutoComplete=""yes"" />
            <Button Name=""BrowseButton"" X=""-11"" Y=""142"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""3"">
                <Text>#(loc.OptionsBrowseButton)</Text>
                <BrowseDirectoryAction VariableName=""InstallFolder"" />
            </Button>
            <RadioButtons Name=""WixStdBAScope"">
                <RadioButton X=""11"" Y=""180"" Height=""20"" Width=""-11"" Value=""PerUser"" EnableCondition=""WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3"" HideWhenDisabled=""yes"" FontId=""3"">#(loc.OptionsPerUserScopeText)</RadioButton>
                <RadioButton X=""11"" Y=""200"" Height=""20"" Width=""-11"" Value=""PerMachine"" EnableCondition=""WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3"" HideWhenDisabled=""yes"" FontId=""3"">#(loc.OptionsPerMachineScopeText)</RadioButton>
            </RadioButtons>
            <Button Name=""OptionsOkButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.OptionsOkButton)</Text>
                <ChangePageAction Page=""Install"" />
            </Button>
            <Button Name=""OptionsCancelButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.OptionsCancelButton)</Text>
                <ChangePageAction Page=""Install"" Cancel=""yes"" />
            </Button>
        </Page>
        <Page Name=""Progress"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">#(loc.ProgressHeader)</Label>
            <Label X=""11"" Y=""121"" Width=""70"" Height=""17"" FontId=""3"" DisablePrefix=""yes"">#(loc.ProgressLabel)</Label>
            <Label Name=""OverallProgressPackageText"" X=""85"" Y=""121"" Width=""-11"" Height=""17"" FontId=""3"" DisablePrefix=""yes"">#(loc.OverallProgressPackageText)</Label>
            <Progressbar Name=""OverallCalculatedProgressbar"" X=""11"" Y=""143"" Width=""-11"" Height=""15"" />
            <Button Name=""ProgressCancelButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">#(loc.ProgressCancelButton)</Button>
        </Page>
        <Page Name=""Modify"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">#(loc.ModifyHeader)</Label>
            <Button Name=""ModifyUpdateButton"" X=""11"" Y=""-11"" Width=""200"" Height=""23"" TabStop=""yes"" FontId=""0"" EnableCondition=""WixStdBAUpdateAvailable"" HideWhenDisabled=""yes"">#(loc.UpdateButton)</Button>
            <Button Name=""RepairButton"" X=""-171"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" HideWhenDisabled=""yes"" ElevationShieldCondition='(WixBundleAuthoredScope = 1 AND NOT WixBundleElevated) OR ((WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3) AND WixStdBAScope = ""PerMachine"")'>#(loc.ModifyRepairButton)</Button>
            <Button Name=""UninstallButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" ElevationShieldCondition='(WixBundleAuthoredScope = 1 AND NOT WixBundleElevated) OR ((WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3) AND WixStdBAScope = ""PerMachine"")'>#(loc.ModifyUninstallButton)</Button>
            <Button Name=""ModifyCancelButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.ModifyCancelButton)</Text>
                <CloseWindowAction />
            </Button>
        </Page>
        <Page Name=""Success"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">
                <Text>#(loc.SuccessHeader)</Text>
                <Text Condition=""WixBundleAction = 2"">#(loc.SuccessLayoutHeader)</Text>
                <Text Condition=""WixBundleAction = 3"">#(loc.SuccessUnsafeUninstallHeader)</Text>
                <Text Condition=""WixBundleAction = 4"">#(loc.SuccessUninstallHeader)</Text>
                <Text Condition=""WixBundleAction = 5"">#(loc.SuccessCacheHeader)</Text>
                <Text Condition=""WixBundleAction = 6"">#(loc.SuccessInstallHeader)</Text>
                <Text Condition=""WixBundleAction = 7"">#(loc.SuccessModifyHeader)</Text>
                <Text Condition=""WixBundleAction = 8"">#(loc.SuccessRepairHeader)</Text>
            </Label>
            <Button Name=""LaunchButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" HideWhenDisabled=""yes"">#(loc.SuccessLaunchButton)</Button>
            <Label X=""-11"" Y=""-51"" Width=""400"" Height=""34"" FontId=""3"" DisablePrefix=""yes"" VisibleCondition=""WixStdBARestartRequired"">
                <Text>#(loc.SuccessRestartText)</Text>
                <Text Condition=""WixBundleAction = 3"">#(loc.SuccessUninstallRestartText)</Text>
            </Label>
            <Button Name=""SuccessRestartButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" HideWhenDisabled=""yes"">#(loc.SuccessRestartButton)</Button>
            <Button Name=""SuccessCloseButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.SuccessCloseButton)</Text>
                <CloseWindowAction />
            </Button>
        </Page>
        <Page Name=""Failure"">
            <Label X=""11"" Y=""80"" Width=""-11"" Height=""30"" FontId=""2"" DisablePrefix=""yes"">
                <Text>#(loc.FailureHeader)</Text>
                <Text Condition=""WixBundleAction = 2"">#(loc.FailureLayoutHeader)</Text>
                <Text Condition=""WixBundleAction = 3"">#(loc.FailureUnsafeUninstallHeader)</Text>
                <Text Condition=""WixBundleAction = 4"">#(loc.FailureUninstallHeader)</Text>
                <Text Condition=""WixBundleAction = 5"">#(loc.FailureCacheHeader)</Text>
                <Text Condition=""WixBundleAction = 6"">#(loc.FailureInstallHeader)</Text>
                <Text Condition=""WixBundleAction = 7"">#(loc.FailureModifyHeader)</Text>
                <Text Condition=""WixBundleAction = 8"">#(loc.FailureRepairHeader)</Text>
            </Label>
            <Hypertext Name=""FailureLogFileLink"" X=""11"" Y=""121"" Width=""-11"" Height=""42"" FontId=""3"" TabStop=""yes"" HideWhenDisabled=""yes"">#(loc.FailureHyperlinkLogText)</Hypertext>
            <Hypertext Name=""FailureMessageText"" X=""22"" Y=""163"" Width=""-11"" Height=""51"" FontId=""3"" TabStop=""yes"" HideWhenDisabled=""yes"" />
            <Label X=""-11"" Y=""-51"" Width=""400"" Height=""34"" FontId=""3"" DisablePrefix=""yes"" VisibleCondition=""WixStdBARestartRequired"">#(loc.FailureRestartText)</Label>
            <Button Name=""FailureRestartButton"" X=""-91"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"" HideWhenDisabled=""yes"">#(loc.FailureRestartButton)</Button>
            <Button Name=""FailureCloseButton"" X=""-11"" Y=""-11"" Width=""75"" Height=""23"" TabStop=""yes"" FontId=""0"">
                <Text>#(loc.FailureCloseButton)</Text>
                <CloseWindowAction />
            </Button>
        </Page>
    </Window>
</Theme>
";
    }
}
