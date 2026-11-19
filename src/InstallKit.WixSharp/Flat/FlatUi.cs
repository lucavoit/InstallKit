using System;
using System.Linq;
using WixSharp;
using WixSharp.UI.WPF;

namespace InstallKit.WixSharp.Flat
{
    /// <summary>
    /// Wires the flat WPF dialogs (<see cref="InstallerUi.ModernFlat"/>) into a managed project: the dialog list, and the
    /// colours, logo and prompted values the dialogs read at install time (as MSI properties and a binary).
    /// </summary>
    internal static class FlatUi
    {
        public static void Apply(ManagedProject project, MsiDefinition msi, InstallerContext context)
        {
            var branding = msi.Branding ?? new Branding();
            var palette = Palette.Create(branding.Theme == InstallerTheme.Light, branding.AccentColor, branding.BackgroundColor, branding.TextColor);

            var hasSettings = msi.PromptedProperties.Count > 0;
            var hasFeatures = msi.Features.Count > 0;
            var pages = string.Join(";", new[] { hasSettings ? "settings" : null, hasFeatures ? "features" : null }.Where(p => p != null));

            var properties = project.Properties.ToList();
            properties.Add(new Property(FlatKeys.Light, branding.Theme == InstallerTheme.Light ? "1" : "0"));
            properties.Add(new Property(FlatKeys.Accent, "#" + palette.Accent.Hex));
            properties.Add(new Property(FlatKeys.Background, "#" + palette.Background.Hex));
            properties.Add(new Property(FlatKeys.Text, "#" + palette.Text.Hex));
            properties.Add(new Property(FlatKeys.License, string.IsNullOrWhiteSpace(msi.LicenseFile) ? "0" : "1"));
            properties.Add(new Property(FlatKeys.Pages, pages.Length > 0 ? pages : "-"));
            if (hasSettings)
            {
                properties.Add(new Property(FlatKeys.Prompts, string.Join(";",
                    msi.PromptedProperties.Select(p => p.Name + "|" + Uri.EscapeDataString(p.Label) + "|" + (p.Required ? "1" : "0")))));
            }

            project.Properties = properties.ToArray();

            var logo = BrandingImages.LogoAsPng(branding, context.IntermediateDirectory, msi.ResolvedOutputFileName);
            if (logo != null)
                project.Binaries = project.Binaries.Concat(new[] { new Binary(new Id(FlatKeys.LogoBinary), logo) }).ToArray();

            var install = new ManagedDialogs().Add<FlatWelcomeDialog>();
            if (hasSettings)
                install.Add<FlatSettingsDialog>();
            if (hasFeatures)
                install.Add<FlatFeaturesDialog>();
            install.Add<FlatProgressDialog>().Add<FlatExitDialog>();

            var modify = new ManagedDialogs().Add<FlatMaintenanceDialog>();
            if (hasFeatures)
                modify.Add<FlatFeaturesDialog>();
            modify.Add<FlatProgressDialog>().Add<FlatExitDialog>();

            project.ManagedUI = new ManagedUI { InstallDialogs = install, ModifyDialogs = modify };

            // The generated background bitmap is for the native dialogs; the flat pages draw their own, so WixSharp's check of the
            // bitmap against its stock dialog layout does not apply.
            project.ValidateBackgroundImage = false;

            // The dialogs live in this assembly, which the MSI has to carry for its UI host to load them.
            var assembly = typeof(FlatUi).Assembly.Location;
            if (!project.DefaultRefAssemblies.Contains(assembly))
                project.DefaultRefAssemblies.Add(assembly);
        }
    }
}
