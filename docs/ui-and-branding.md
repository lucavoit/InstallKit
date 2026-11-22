# UI and branding

← [Asking for values](prompted-values.md) · [Docs index](README.md) · [Bundles](bundles.md) →

With no `Appearance`, an MSI gets **the best UI its engine has**: the flat, dark WPF dialogs with the `WixSharp` engine, the native modern dialogs with the `WixSharpCore` engine. Pin one with `Appearance`:

| `Ui` | Dialogs |
|---|---|
| `Default` (what you get) | `ModernFlat` with the `WixSharp` engine, `ModernInstallDir` with `WixSharpCore`; no warning, because nothing was chosen |
| `ModernFlat` | Flat WPF dialogs: big titles, flat accent buttons, custom check box and path box, a caption in the page colour (`Appearance(a => a.Flat())`; `WixSharp` engine only) |
| `Modern` | Native, branded: one welcome page (with the license and its accept box when set), progress, finish; repair/remove |
| `ModernInstallDir` | `Modern` with the install folder and a Browse button on the page after the welcome page |
| `ModernWpf` | The stock WPF dialogs, which allow custom XAML dialogs (`WixSharp` engine only) |
| `Minimal` | Classic WiX: welcome/license, progress |
| `InstallDir`, `FeatureTree`, `ProgressOnly` | Classic WiX variants |

```csharp
MsiBuilder.Create("My App")
    .Appearance(a => a
        .Flat()                     // or Modern(chooseInstallDir: true), or Wpf(); leave it out for the default
        .Theme(InstallerTheme.Auto) // Dark (the default), Light, or Auto: follow the Windows light/dark setting
        .AccentColor("#0067C0")
        .BackgroundColor("#0F1115") // optional: overrides the theme's page colour
        .TextColor("#F3F4F6")       // optional: overrides the theme's text colour
        .Logo("logo.png"));
```

- **Colours:** the modern dialogs and the bundle window are dark by default. `Theme` picks `Dark`, `Light` or `Auto`, and `BackgroundColor`, `TextColor` and `AccentColor` (all `#RRGGBB`) override single colours; secondary text and input boxes are derived from them. Colours you set yourself always win over the theme.
- **`Theme = Auto`:** follows the Windows app mode (Settings, Personalization, Colors) of the person running the setup, read when it starts. Only the flat UI can do that. The native dialogs and the bundle window draw their colours when they are built, so they use dark and the build prints warning `IK204`.
- **The flat UI (`ModernFlat`):** every page is drawn by WPF in your theme and colours, so it looks the same on every machine: a big title, flat accent buttons, a custom check box and path box, a thin scrollbar, and a window caption that follows the page colour on Windows 11 (older Windows keeps its standard caption). It shows the license (when set; the accept box sits above the text) on the welcome page (with buttons to open it in another program or save a copy), then the install folder and prompted values on one page, and optional features, progress, and repair/remove. It needs .NET Framework 4.8 on the target machine (part of Windows 10 1903+ and Windows 11) and adds about 1 MB to the MSI. The window frame is still Windows' own, and the folder picker that Browse opens is a standard Windows dialog. Its texts are English for now.
- **Native dialogs (`Modern`, `ModernInstallDir`):** built from MSI's own dialog tables, in Segoe UI on the branded background. The target machine needs nothing extra, and it works the same on both engines. Windows draws their buttons, input boxes and checkboxes, so those keep the system look. The feature page, the folder browser and the error popup are WiX's own light dialogs; the progress, repair/remove, resume, cancel and finish pages are restyled to match. Its texts are English for now.
- **Branded images:** InstallKit generates the banner (493×58), the dialog (493×312) and page bitmaps in your accent colour with your logo, so even the classic dialogs look current. You can supply your own `.bmp` files through the `BannerImage` and `BackgroundImage` settings of `Branding`. Bundles use the logo too.
- **Bundles:** the setup window is dark by default and follows `Theme`, `AccentColor`, `BackgroundColor` and `TextColor` (but not `Auto`). Its buttons and progress bar are drawn by Windows.
- **`ModernWpf`:** needs .NET Framework 4.8 on the target machine and adds about 1 MB to the MSI. The `WixSharpCore` engine falls back to `Modern` with warning `IK200`; an explicit `Flat()` falls back to `ModernInstallDir` the same way.
- Silent installs (`/qn`) show no UI at all.

**Your own dialog (`ModernWpf`):** write a XAML `UserControl` that derives from `WixSharp.UI.WPF.WpfDialog` and implements `IWpfDialog`, and set `<InstallKitUseWpf>true</InstallKitUseWpf>` in the project. Then add it:

```csharp
using InstallKit.WixSharp;
MsiBuilder.Create("My App")
    .Appearance(a => a.Wpf())
    .AddWpfDialog<ServerSettingsDialog>(after: ModernDialog.Welcome);
```

The dialog stores what the user enters in MSI properties, and your custom actions read them with `e["SERVER"]`. Custom dialogs are the one WixSharp-specific part of the UI, so keep them in a separate file.

---
← [Asking for values](prompted-values.md) · [Docs index](README.md) · [Bundles](bundles.md) →
