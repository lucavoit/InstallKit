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
        public static void Apply(Project project, MsiDefinition msi)
        {
            var options = new ModernDialogOptions
            {
                LicenseFile = string.IsNullOrWhiteSpace(msi.LicenseFile) ? null : Path.GetFullPath(msi.LicenseFile),
                ChooseInstallDir = msi.Ui == InstallerUi.ModernInstallDir,
                HasFeatures = msi.Features.Count > 0,
            };

            project.WixSourceGenerated += document =>
            {
                var root = document.Root!;
                XNamespace ns = root.Name.Namespace;

                // WixSharp references WixUI_Minimal (which also pulls in the UI extension); swap in ours.
                foreach (var uiRef in root.Descendants(ns + "UIRef").Where(e => (string?)e.Attribute("Id") == "WixUI_Minimal").ToList())
                    uiRef.SetAttributeValue("Id", ModernDialogXml.UiId);

                root.Add(ModernDialogXml.BuildFragment(ns, options));
            };
        }
    }
}
