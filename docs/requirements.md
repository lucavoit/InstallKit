# Requirements (launch conditions)

← [Package platform](platform.md) · [Docs index](README.md) · [Editor checks](editor-checks.md) →

A launch condition stops the setup with a message when the machine does not meet it. They apply to plain MSIs as well as to the MSI inside a bundle (a bundle's own prerequisites are described in [Prerequisites](prerequisites.md)).

```csharp
MsiBuilder.Create("My App")
    .Requirements(r => r
        .Requires64Bit()                       // 64-bit Windows
        .RequiresWindows(19041)                // Windows 10 2004 or later (build number)
        .RequiresNetFramework(NetFrameworkVersion.NetFx48)   // .NET Framework 4.6.2, 4.7.2, 4.8 or 4.8.1
        .RequiresWebView2()                    // the Edge WebView2 Runtime, for the machine or the user
        .LaunchCondition("MYPROP = 1", "Set MYPROP=1 on the command line."));
```

- **`Requires64Bit()` and `RequiresWindows(build)`** are written as `Installed OR (...)`, so a product that is already installed can still be repaired and removed on a machine that no longer meets them. A condition passed to `LaunchCondition(condition, message)` is used exactly as written: add `Installed OR (...)` yourself when that matters.
- **`RequiresNetFramework(version)`** reads the `Release` value of the .NET Framework 4.x key into a property and compares it with the first release of that version. **`RequiresWebView2()`** checks the WebView2 `pv` value for the machine and for the current user. Both are skipped when the product is already installed. To *install* these with the product instead of only requiring them, chain them in a bundle ([Prerequisites](prerequisites.md)).
- **Messages:** every helper takes an optional message; the default tells the user what is missing. A custom condition needs a condition and a message (`IK320`, `IK321`).
- **Windows builds:** `19041` is Windows 10 2004, `19045` Windows 10 22H2, `22000` Windows 11.

## .NET Framework 4.8 is added for you

The flat dialogs (the default with the `WixSharp` engine), the WPF dialogs and the install-event handlers run on the **.NET Framework 4.8** of the target machine. It is part of Windows 10 1903 and later, but missing on older Windows 10 builds without an update. When the MSI uses any of them, the `WixSharp` engine adds a launch condition on its own (`Installed OR (INSTALLKIT_NETFX_RELEASE >= "#528040")`, read from the registry) with a message that names .NET Framework 4.8 and where to get it, so the setup says what is wrong instead of failing obscurely. The `WixSharpCore` engine compiles handlers to native code and needs nothing, so it adds no condition. See also [What target machines need](target-machines.md).

---
← [Package platform](platform.md) · [Docs index](README.md) · [Editor checks](editor-checks.md) →
