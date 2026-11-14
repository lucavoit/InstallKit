using System;
using WixSharp;

namespace InstallKit.WixSharp
{
    /// <summary>Stock pages of the WPF dialog set, used as anchors for custom dialogs.</summary>
    public enum ModernDialog
    {
        Welcome,
        License,
        InstallDir,
        Progress,
        Finish,
    }

    /// <summary>A custom WPF dialog inserted into the modern dialog set.</summary>
    internal sealed class WpfDialogInsertion
    {
        public WpfDialogInsertion(Type dialogType, ModernDialog after)
        {
            DialogType = dialogType;
            After = after;
        }

        public Type DialogType { get; }

        public ModernDialog After { get; }
    }

    public static class WpfDialogExtensions
    {
        /// <summary>
        /// Adds your own WPF dialog to the WPF UI (<see cref="InstallerUi.ModernWpf"/>). The dialog is a XAML
        /// UserControl deriving from <c>WixSharp.UI.WPF.WpfDialog</c> and implementing <c>IWpfDialog</c>; set
        /// <c>InstallKitUseWpf</c> in the project to compile XAML. WixSharp-specific: an engine without WPF
        /// dialogs ignores it with a warning.
        /// </summary>
        public static MsiDefinition AddWpfDialog<TDialog>(this MsiDefinition msi, ModernDialog after = ModernDialog.Welcome)
            where TDialog : IDialog
        {
            msi.EngineCustomizations.Add(new WpfDialogInsertion(typeof(TDialog), after));
            return msi;
        }

        /// <inheritdoc cref="AddWpfDialog{TDialog}(MsiDefinition, ModernDialog)"/>
        public static MsiBuilder AddWpfDialog<TDialog>(this MsiBuilder msi, ModernDialog after = ModernDialog.Welcome)
            where TDialog : IDialog =>
            msi.WithEngineCustomization(new WpfDialogInsertion(typeof(TDialog), after));
    }
}
