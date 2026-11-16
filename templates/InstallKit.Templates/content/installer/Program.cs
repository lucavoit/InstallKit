using InstallKit;

return InstallerHost.Run(args, plan =>
{
    var app = plan.Msi(MsiBuilder.Create("My Product")
        .WithManufacturer("My Company")
        .WithUpgradeCode("11111111-1111-1111-1111-111111111111") // generated for you; never change it
        .WithMainExecutable("MyAppPayload.exe")
        .Content(c => c.Payload("MyAppPayload"))
        .Shell(s => s.DesktopShortcut()));
#if (bundle)

    plan.Bundle(BundleBuilder.Create("My Product")
        .WithManufacturer("My Company")
        .WithUpgradeCode("22222222-2222-2222-2222-222222222222")
        .Packages(k => k.Msi(app)));
#endif
});
