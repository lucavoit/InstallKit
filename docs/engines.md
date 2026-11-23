# Engines

← [What target machines need](target-machines.md) · [Docs index](README.md) · [WiX version and licence](wix-licence.md) →

An installer project uses **one** engine, chosen with a single property. Your installer code stays the same either way:

```xml
<PropertyGroup>
  <InstallKitEngine>WixSharpCore</InstallKitEngine>  <!-- default: WixSharp -->
</PropertyGroup>
```

| | `WixSharp` (default) | `WixSharpCore` (preview) |
|---|---|---|
| Package | `InstallKit.WixSharp` → WixSharp_wix4 | `InstallKit.WixSharp.Core` → WixSharp.Core |
| Installer project targets | .NET Framework 4.8 | .NET 10 (`net10.0-windows`); WixSharp.Core itself ships for .NET 8 and runs on 10 |
| MSIs, services, shortcuts, run commands, branding, bundles, signing | ✓ | ✓ |
| Install events (`OnAfterInstall`, ...) | ✓ (managed custom actions) | ✓ (compiled to native code; the build machine needs the C++ build tools) |
| Modern UI (native) | ✓ | ✓ |
| Default UI | flat WPF dialogs | native modern dialogs |
| `ModernFlat`, `ModernWpf`, custom WPF dialogs | ✓ | not yet: fall back to the native modern dialogs (`IK200`) |

**Build agent requirements:** either engine needs only Windows and the .NET SDK. The SDK pulls in just the engine package you chose, so the other engine is never restored or built. The .NET Framework 4.8 reference assemblies come from NuGet, so the `WixSharp` engine needs no targeting pack. The pipeline templates are the same for both.

**Code shared by both:**
- **One engine source:** `InstallKit.WixSharp.Core` compiles the same engine source as `InstallKit.WixSharp`, with `INSTALLKIT_CORE` defined, so the two engines can't drift apart.
- **Same names:** both use the same namespace and type names, so `using InstallKit.WixSharp;` and `ConfigureWixSharp` work with either. WixSharp.Core mirrors WixSharp's API, so most raw WixSharp code carries over too.
- **Adapting your installer code:** the SDK defines `INSTALLKIT_ENGINE_WIXSHARP` or `INSTALLKIT_ENGINE_WIXSHARPCORE`. The sample uses this to register its install event only on the .NET Framework engine.

**Core engine notes:**
- **Native compilation:** WixSharp.Core compiles the installer assembly's custom actions to a native DLL with NativeAOT. That needs the C++ build tools ("Desktop development with C++") on the build machine, which Microsoft-hosted Windows agents have. It also adds roughly 10–60 seconds per build.
- **Remaining gap:** the WPF dialogs (the flat UI and custom dialogs). WixSharp.Core needs them in a separate .NET Framework UI project; the default UI there is the native modern dialogs.

`samples/HelloService.Installer.Core` builds the same sample with the Core engine. CI builds both.

---
← [What target machines need](target-machines.md) · [Docs index](README.md) · [WiX version and licence](wix-licence.md) →
