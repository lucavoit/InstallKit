# Roadmap

What InstallKit may offer next, grouped by when. This is a plan, not a promise: the order can change with feedback. Open an issue to argue for moving something up.

## Done

Not released yet:
- Registry values, with removal on uninstall
- `PATH` entries and environment variables
- File associations and URL protocol handlers
- Firewall rules (inbound)
- Values in XML config files, set at install time without a custom action (JSON is still to do)
- Closing (and optionally ending) running programs before an install, upgrade or uninstall

## Now

In progress or next up.

**Installer model** (fluent builders, engine-neutral, validated before WiX runs):
- Multi-instance (side-by-side) installs and `.mst` transforms for per-customer variants
- Optional features: a builder API for feature groups
- Prerequisite helpers for bundles: .NET runtime, VC++ redistributable, WebView2, with built-in detect conditions

**Tooling and runtime:**
- Roslyn analyzers and code fixes for the `IKxxx` validation errors
- An `installkit` dotnet tool: `inspect`, `diff` and `verify` for MSI files, plus `new-guid`
- Folder permissions (grant an account rights on the install folder or a subfolder)

## Later

- Service extras: recovery and failure actions, dependencies, delayed auto-start, virtual service account and gMSA
- Localization: multi-language dialogs and MSI language transforms
- MSIX / App Installer as a third output format
- More templates: service installer, WPF app, per-user tray app, Core-engine template
- Install-time test helpers: a library that installs, upgrades, repairs and uninstalls, and snapshots files and the registry
- A GitHub Actions reusable workflow that mirrors the Azure DevOps templates
- Reproducible builds: deterministic GUIDs and timestamps
- Validate-only mode: build the plan and run the validators on Linux and macOS
- Opt-in telemetry hooks for install success and failure

## In the future

- Per-user (non-elevated) installs, verified end to end with QA coverage
- Start-with-Windows entries and scheduled tasks for tray apps
- `.msp` patches and delta or minor-upgrade support
- Auto-update: an update-check helper or a generated update feed
- Package manager manifests: winget, Chocolatey and Scoop
- Dry-run plan output as JSON or Markdown
- Loading the plan from YAML or JSON
- A docs site with recipes
- GitLab CI and Jenkins snippets
- SBOM and provenance (SPDX or CycloneDX, SLSA attestations)
- An `InstallKit.Runtime` library for installed apps

## Not planned

- A portable zip output
- Release automation (tag, build, sign, publish)
