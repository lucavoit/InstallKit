# Contributing

← [Project layout](project-layout.md) · [Docs index](README.md) · [QA](qa.md) →

Issues and pull requests are welcome. Please open an issue first for larger changes. [CI](../.github/workflows/ci.yml) runs on every pull request and push to `main`: unit tests, packing, both sample installers and the end-to-end QA. Run the unit tests locally with:

```bash
dotnet test tests/InstallKit.Abstractions.Tests -c Release    # model, builders, validation, analyzers
dotnet test tests/InstallKit.WixSharp.Tests -c Release        # the native dialogs' XML, the bundle theme, the palette
dotnet test tests/InstallKit.WixSharp.Engine.Tests -c Release # the engine: the WiX source it writes for a model
dotnet test tests/InstallKit.Tool.Tests -c Release            # the installkit tool
```

The engine tests compile the engine source into a test project and read the WiX source that comes out, so they need neither Windows nor WiX. They guard what used to be found only by the Windows CI run.

---
← [Project layout](project-layout.md) · [Docs index](README.md) · [QA](qa.md) →
