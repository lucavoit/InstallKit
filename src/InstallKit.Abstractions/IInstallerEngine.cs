using System;
using System.Collections.Generic;

namespace InstallKit
{
    /// <summary>
    /// Turns the engine-neutral model into real installers. The WixSharp (.NET Framework) engine is the
    /// first implementation; a WixSharp.Core engine can be added later without changing user code.
    /// </summary>
    public interface IInstallerEngine
    {
        /// <summary>Short name used in log output.</summary>
        string Name { get; }

        /// <summary>Optional features this engine supports.</summary>
        InstallerCapabilities Capabilities { get; }

        /// <summary>Builds an MSI and returns the path of the .msi file.</summary>
        string BuildMsi(MsiDefinition msi, InstallerContext context);

        /// <summary>Builds a bundle and returns the path of the setup .exe.</summary>
        /// <param name="bundle">The bundle to build.</param>
        /// <param name="builtMsis">Paths of the MSIs built earlier in this run, for chained <see cref="MsiDefinition"/>s.</param>
        /// <param name="context">The build inputs.</param>
        string BuildBundle(BundleDefinition bundle, IReadOnlyDictionary<MsiDefinition, string> builtMsis, InstallerContext context);
    }

    /// <summary>
    /// Names the engine type the entry assembly uses. The InstallKit SDK adds this attribute from the
    /// <c>InstallKitEngine</c> MSBuild property, so switching engines is a one-line project change.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class InstallerEngineAttribute : Attribute
    {
        public InstallerEngineAttribute(string engineTypeName) => EngineTypeName = engineTypeName;

        /// <summary>Assembly-qualified type name of an <see cref="IInstallerEngine"/> with a parameterless constructor.</summary>
        public string EngineTypeName { get; }
    }
}
