using System;
using System.Collections.Generic;

namespace InstallKit
{
    /// <summary>Collects the MSIs and bundles an installer project produces.</summary>
    public sealed class InstallerPlan
    {
        readonly List<MsiDefinition> msis = new List<MsiDefinition>();
        readonly List<BundleDefinition> bundles = new List<BundleDefinition>();

        public InstallerPlan(InstallerContext context) => Context = context;

        public InstallerContext Context { get; }

        public IReadOnlyList<MsiDefinition> Msis => msis;

        public IReadOnlyList<BundleDefinition> Bundles => bundles;

        /// <summary>Signs every built MSI and bundle with your own signer (overrides InstallKitSignCommand).</summary>
        public InstallerPlan UseSigner(IFileSigner signer)
        {
            Context.Signer = signer ?? throw new ArgumentNullException(nameof(signer));
            return this;
        }

        /// <summary>Adds an MSI. Keep the returned definition to chain it into a bundle.</summary>
        public MsiDefinition Msi(string name, Action<MsiDefinition> configure)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An MSI needs a name.", nameof(name));

            var msi = new MsiDefinition(name);
            configure(msi);
            msis.Add(msi);
            return msi;
        }

        /// <summary>Adds an MSI built with the fluent <see cref="MsiBuilder"/>. Keep the result to chain it into a bundle.</summary>
        public MsiDefinition Msi(MsiBuilder builder)
        {
            var msi = (builder ?? throw new ArgumentNullException(nameof(builder))).Build();
            if (string.IsNullOrWhiteSpace(msi.Name))
                throw new ArgumentException("An MSI needs a name. Start builders with MsiBuilder.Create(\"Name\").", nameof(builder));

            if (!msis.Contains(msi))
                msis.Add(msi);
            return msi;
        }

        /// <summary>Adds a bundle built with the fluent <see cref="BundleBuilder"/>.</summary>
        public BundleDefinition Bundle(BundleBuilder builder)
        {
            var bundle = (builder ?? throw new ArgumentNullException(nameof(builder))).Build();
            if (string.IsNullOrWhiteSpace(bundle.Name))
                throw new ArgumentException("A bundle needs a name. Start builders with BundleBuilder.Create(\"Name\").", nameof(builder));

            if (!bundles.Contains(bundle))
                bundles.Add(bundle);
            return bundle;
        }

        /// <summary>Adds a bundle (setup .exe). It is built after every MSI.</summary>
        public BundleDefinition Bundle(string name, Action<BundleDefinition> configure)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A bundle needs a name.", nameof(name));

            var bundle = new BundleDefinition(name);
            configure(bundle);
            bundles.Add(bundle);
            return bundle;
        }
    }
}
