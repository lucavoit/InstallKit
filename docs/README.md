# InstallKit documentation

Technical documentation, one topic per page. Every page has previous/next links; start with [Getting started](getting-started.md). Back to the [project README](../README.md).

## Start here

- [Getting started](getting-started.md): Install the template, pin WiX and build your first MSI.
- [Builders](builders.md): The fluent builders and their groups.
- [Using your own build output](own-build-output.md): Build an installer from files your pipeline already produced.
- [How it works](how-it-works.md): What `dotnet build` does, MSBuild properties and the raw WixSharp escape hatch.

## Describe the installer

- [Custom actions](custom-actions.md): C# code that runs on the target machine during install.
- [Registry](registry.md): Registry values.
- [Environment variables and PATH](environment-variables.md): Environment variables and PATH entries.
- [File associations and URL protocols](file-associations.md): File associations and URL protocols.
- [Firewall rules](firewall-rules.md): Inbound firewall rules.
- [Values in XML config files](xml-values.md): Set values in XML config files at install time.
- [Closing running programs](closing-programs.md): Close or end programs before install, upgrade or uninstall.
- [Optional features](optional-features.md): Features users can leave out.
- [Folder permissions](folder-permissions.md): Grant rights on folders of the product.
- [Asking for values](prompted-values.md): Ask for values in the installer UI.
- [UI and branding](ui-and-branding.md): The default UI, themes, colours, native and custom dialogs.
- [Bundles](bundles.md): Single setup .exe that chains packages.
- [Prerequisites](prerequisites.md): Runtimes chained into a bundle.
- [Signing](signing.md): When and how files get signed.
- [Versions side by side](side-by-side.md): Install several versions next to each other.
- [Package platform](platform.md): x64, x86 or Arm64 packages and what they change.
- [Requirements (launch conditions)](requirements.md): Require Windows versions, 64-bit Windows or a custom condition.

## Tooling

- [Editor checks](editor-checks.md): Roslyn analyzers (IK300-IK303).
- [Command line tool](command-line-tool.md): The installkit tool: new-guid, inspect, diff.

## Ship it

- [Azure DevOps](azure-devops.md): Pipeline templates.
- [What target machines need](target-machines.md): What must be on the machine that runs the installer.
- [Engines](engines.md): WixSharp and WixSharp.Core engines.
- [WiX version and licence](wix-licence.md): Pinned WiX version, OSMF and EULA.

## Project

- [Project layout](project-layout.md): Folders of this repository.
- [Contributing](contributing.md): Issues, pull requests and unit tests.
- [QA](qa.md): End-to-end and UI tests.

Also: [Open issues](https://github.com/lucavoit/InstallKit/issues) · [AI skill](../skills/installkit/SKILL.md) (for coding agents)

## Using the AI skill

`skills/installkit/` is a skill in the Claude Code format (`SKILL.md` plus `references/`). Copy the folder to `.claude/skills/installkit/` in a repository (or `~/.claude/skills/installkit/` for all your projects) and coding agents that read skills learn how to write InstallKit installers, which rules to keep and what the `IKxxx` messages mean. It links back to these pages instead of repeating them.
