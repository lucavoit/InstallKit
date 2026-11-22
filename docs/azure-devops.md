# Azure DevOps

← [Command line tool](command-line-tool.md) · [Docs index](README.md) · [What target machines need](target-machines.md) →

The templates are layered so you can use a whole stage or individual steps:

```
pipelines/
  steps/setup-tools.yml        UseDotNet + dotnet tool restore
  steps/build-installer.yml    dotnet build with version, output dir and sign command
  steps/smoke-test.yml         msiexec install + uninstall every MSI, publish the logs
  steps/publish-universal.yml  publish a folder to Azure Artifacts as a Universal Package
  jobs/build-installer.yml     Windows job: tools → preBuildSteps → build → postBuildSteps → smoke test → artifact
  stages/installer.yml         Build stage + Publish stage (skipped for pull requests)
```

```yaml
resources:
  repositories:
    - repository: installkit
      type: github
      name: lucavoit/InstallKit
      endpoint: github
      ref: refs/tags/installkit-v0.1.0

stages:
  - template: pipelines/stages/installer.yml@installkit
    parameters:
      setupProject: src/MyApp.Installer/MyApp.Installer.csproj
      version: 1.2.$(Build.BuildId)
      feed: MyProject/installers      # Azure Artifacts feed
      packageName: my-app
      preBuildSteps:                  # optional: your signing setup
        - task: AzureCLI@2
          inputs: { ... }
      signCommand: AzureSignTool sign ... {file}
```

Download a published installer with `az artifacts universal download --feed installers --name my-app --version 1.2.345 --path .`.

These templates are for your own pipelines. InstallKit itself is built with GitHub Actions; see [Contributing](contributing.md).

---
← [Command line tool](command-line-tool.md) · [Docs index](README.md) · [What target machines need](target-machines.md) →
