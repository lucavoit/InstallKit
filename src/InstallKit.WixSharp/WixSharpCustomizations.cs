using System;
using WixSharp;
using WixSharp.Bootstrapper;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Escape hatch to the raw WixSharp objects, applied right before WiX runs. Anything that uses
    /// these ties that piece of the installer to the WixSharp engine, so prefer the InstallKit model.
    /// </summary>
    public static class WixSharpCustomizations
    {
        public static MsiDefinition ConfigureWixSharp(this MsiDefinition msi, Action<Project> configure)
        {
            msi.EngineCustomizations.Add(configure);
            return msi;
        }

        public static BundleDefinition ConfigureWixSharp(this BundleDefinition bundle, Action<Bundle> configure)
        {
            bundle.EngineCustomizations.Add(configure);
            return bundle;
        }

        public static MsiBuilder ConfigureWixSharp(this MsiBuilder msi, Action<Project> configure) =>
            msi.WithEngineCustomization(configure);

        public static BundleBuilder ConfigureWixSharp(this BundleBuilder bundle, Action<Bundle> configure) =>
            bundle.WithEngineCustomization(configure);
    }
}
