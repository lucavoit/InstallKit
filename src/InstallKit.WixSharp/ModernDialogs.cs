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
    /// accept box above it (Next waits for it), and an Install (or Next) button.</item>
    /// <item>With <see cref="InstallerUi.ModernInstallDir"/> or prompted properties, a page after it with the install folder and Browse
    /// button above the prompted values.</item>
    /// <item>With optional features the welcome page leads to the feature page and a "ready to install" page; the repair
    /// dialog then also offers Change.</item>
    /// <item>A finish page on the same background.</item>
    /// <item>Progress, errors, files-in-use and repair/remove reuse the WiX UI extension's dialogs, which pick
    /// up the generated banner and the Segoe UI text styles defined here.</item>
    /// </list>
    /// Implemented as a WiX UI fragment (see <see cref="ModernDialogXml"/>) injected into the generated source in place of WixUI_Minimal.
    /// </summary>
    internal static class ModernDialogs
    {
        public static void Apply(Project project, MsiDefinition msi, string? pageBackground = null, string? cancelBackground = null)
        {
            var options = new ModernDialogOptions
            {
                LicenseFile = string.IsNullOrWhiteSpace(msi.LicenseFile) ? null : Path.GetFullPath(msi.LicenseFile),
                ChooseInstallDir = msi.Ui == InstallerUi.ModernInstallDir,
                HasFeatures = msi.Features.Count > 0,
                Palette = Palette.Create(msi.Branding?.Theme == InstallerTheme.Light, msi.Branding?.AccentColor, msi.Branding?.BackgroundColor, msi.Branding?.TextColor),
                PageBackground = pageBackground,
                CancelBackground = cancelBackground,
                Prompts = msi.PromptedProperties.Select(p => new ModernPrompt(p.Name, p.Label, p.Required)).ToList(),
            };

            project.WixSourceGenerated += document =>
            {
                var root = document.Root!;
                XNamespace ns = root.Name.Namespace;

                // WixSharp selects WixUI_Minimal, and swapping it for ours is what makes our fragment part of the MSI: WiX
                // leaves out a fragment nothing references. Depending on the WiX version WixSharp writes it as a UIRef
                // or as a WixUI element of the UI extension's namespace (WiX 4 and later), so look for both by name.
                var stock = root.Descendants()
                    .Where(e => (e.Name.LocalName == "UIRef" || e.Name.LocalName == "WixUI") && (string?)e.Attribute("Id") == "WixUI_Minimal")
                    .ToList();
                foreach (var element in stock)
                    element.ReplaceWith(new XElement(ns + "UIRef", new XAttribute("Id", ModernDialogXml.UiId)));

                root.Add(ModernDialogXml.BuildFragment(ns, options));
            };
        }
    }
}
