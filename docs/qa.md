# QA

← [Contributing](contributing.md) · [Docs index](README.md)

The `qa` jobs in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) build the QA demo installer as 1.0.0 and 1.1.0 with each engine, then run [`qa/tests/Installer.Tests.ps1`](../qa/tests/Installer.Tests.ps1) on a GitHub-hosted Windows runner. It checks:

- **Install:** the Add/Remove Programs entry, payload and extra files, Start menu and desktop shortcuts, the running service, the `OnAfterInstall` handler and `RunAfterInstall` command, and a property passed on the command line.
- **Upgrade:** 1.1.0 replaces 1.0.0, and `RunBeforeUninstall` does not run.
- **Repair:** a deleted file is restored.
- **Uninstall:** `RunBeforeUninstall` runs, and the service, files, shortcuts and Add/Remove Programs entry are removed.
- **Bundle:** `QaDemoSetup.exe` installs and uninstalls the same product.

The `ui` jobs drive the real setup dialogs with UI Automation ([`qa/UiTests`](../qa/UiTests), FlaUI): they click through install, repair, remove and cancel, check what each page shows, and **take a screenshot of every page**. They run the native dialogs on both engines, the flat UI in dark, light and `Auto` (with the Windows app mode set to light and to dark), and the bundle window. Open a run in the Actions tab and download the `ui-*` artifacts to look at the pictures, the UI trees and the msiexec logs.

A separate `consumer` job checks the route a user without a monorepo takes. [`qa/consumer/Build-Consumer.ps1`](../qa/consumer/Build-Consumer.ps1) creates an installer project outside this repository from the packed NuGet packages and the `dotnet new installkit` template, points it at a folder of prebuilt files, and builds an MSI and a bundle. [`qa/tests/Consumer.Tests.ps1`](../qa/tests/Consumer.Tests.ps1) then installs and removes it. This is the test that proves the packages work without this repository.

The tests change the machine, so only run them on a disposable one. Locally, from an elevated PowerShell:

```powershell
dotnet build qa/QaDemo.Installer -c Release -p:InstallKitBuildOnBuild=true -p:InstallKitVersion=1.0.0 -p:InstallKitOutputDir=$PWD/out/1.0.0/
dotnet build qa/QaDemo.Installer -c Release -p:InstallKitBuildOnBuild=true -p:InstallKitVersion=1.1.0 -p:InstallKitOutputDir=$PWD/out/1.1.0/
./qa/tests/Invoke-Qa.ps1 -V1 out/1.0.0 -V2 out/1.1.0
```

---
← [Contributing](contributing.md) · [Docs index](README.md)
