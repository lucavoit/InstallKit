# Firewall rules

← [File associations and URL protocols](file-associations.md) · [Docs index](README.md) · [Values in XML config files](xml-values.md) →

```csharp
MsiBuilder.Create("My App")
    .Machine(m => m
        .Service("MyService.exe", "MyService")
        .FirewallRule("My App web", "8080")                                  // TCP port, any program
        .FirewallRule("My App ports", "8000-8100", FirewallProtocol.Udp)     // a range, or a list such as "80,443"
        .FirewallRule("My App", executable: "MyService.exe",                 // any port of one installed program,
            scope: FirewallScope.LocalSubnet, profile: FirewallProfile.Private)); // only from the local subnet, on private networks
```

- **Inbound:** the rule allows inbound connections and is removed on uninstall. Outbound rules are not supported yet.
- **Needs elevation:** a per-user install cannot create firewall rules (`IK214`).
- **Checked before WiX runs:** a rule needs a name (`IK210`) and a port or a program (`IK211`); ports must be 1 to 65535, a range or a list (`IK212`); a port needs TCP or UDP (`IK213`); the program must be in a payload (`IK121`).
- **WiX extension:** the build adds the WiX firewall extension automatically when a rule is used; it is pinned to the same version as the `wix` tool like the other extensions.

---
← [File associations and URL protocols](file-associations.md) · [Docs index](README.md) · [Values in XML config files](xml-values.md) →
