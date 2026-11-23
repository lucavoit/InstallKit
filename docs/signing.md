# Signing

← [Prerequisites](prerequisites.md) · [Docs index](README.md) · [Versions side by side](side-by-side.md) →

InstallKit leaves the signing tool to you; it only decides when files get signed:

1. Each MSI is signed right after it is built, before any bundle embeds it.
2. For a bundle, the Burn engine is detached and signed, then reattached (`wix burn detach/reattach`), and then the bundle itself is signed.

Signing a bundle `.exe` after the build would leave its engine and the MSIs inside it unsigned, which is why signing happens during the build. You can plug in your tool in either of two ways:

- **Command:** set `InstallKitSignCommand` (or the `INSTALLKIT_SIGN_COMMAND` environment variable). It runs once per file, with `{file}` replaced by the quoted path:
  `AzureSignTool sign -kvu https://my-vault.vault.azure.net -kvm -kvc my-cert -tr http://timestamp.digicert.com {file}`
- **Code:** `plan.UseSigner(new MySigner())` with your own `IFileSigner`.

---
← [Prerequisites](prerequisites.md) · [Docs index](README.md) · [Versions side by side](side-by-side.md) →
