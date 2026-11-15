using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InstallKit.WixSharp.Engine.Tests;

/// <summary>What the engine writes for an MSI. Every test below guards something that broke in a real build at some point.</summary>
public class MsiEngineTests
{
    static string? A(XElement element, string name) => (string?)element.Attribute(name);

    [Fact]
    public void The_install_directory_has_its_id_and_sits_under_the_manufacturer_in_program_files()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithMainExecutable("App.exe"));

        var installDir = wxs.One("Directory", "INSTALLDIR");
        Assert.Equal("My App", A(installDir, "Name"));
        Assert.Equal("Contoso", A(installDir.Parent!, "Name"));
        Assert.Equal("ProgramFiles64Folder", A(installDir.Parent!.Parent!, "Id"));
        Assert.Contains(installDir.Descendants().Where(e => e.Name.LocalName == "File"), f => (A(f, "Source") ?? "").EndsWith("App.exe"));
    }

    [Fact]
    public void Registry_values_are_top_level_and_keep_their_types()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App()
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "InstallPath", "[INSTALLDIR]")
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", null, "default")
            .WithRegistryValue(RegistryHive.CurrentUser, @"Software\Contoso\App", "Logs", @"%LOCALAPPDATA%\Logs", RegistryValueKind.ExpandString)
            .WithRegistryValue(RegistryHive.LocalMachine, @"Software\Contoso\App", "Telemetry", 0));

        // WixSharp rejects registry values inside a directory, so they are components of their own.
        Assert.DoesNotContain(wxs.One("Directory", "INSTALLDIR").Descendants(), e => e.Name.LocalName == "RegistryKey");

        var values = wxs.All("RegistryValue").ToDictionary(v => A(v, "Id")!);
        Assert.Equal("string", A(values["InstallKitReg1"], "Type"));
        Assert.Null(A(values["InstallKitReg2"], "Name"));            // the default value has no name
        Assert.Equal("expandable", A(values["InstallKitReg3"], "Type"));
        Assert.Equal("integer", A(values["InstallKitReg4"], "Type"));
        Assert.Equal("0", A(values["InstallKitReg4"], "Value"));
    }

    [Fact]
    public void A_percent_in_a_string_value_does_not_turn_it_into_an_expandable_value()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithMainExecutable("App.exe").WithFileAssociation(".foo", "Foo document").WithUrlProtocol("myapp", "My App link"));

        var commands = wxs.All("RegistryValue").Where(v => (A(v, "Value") ?? "").Contains("%1")).ToList();
        Assert.Equal(2, commands.Count);
        Assert.All(commands, v => Assert.Equal("string", A(v, "Type")));
        Assert.Contains(wxs.All("RegistryValue"), v => A(v, "Name") == "URL Protocol" && A(v, "Value") == "");
        Assert.Contains(wxs.All("RegistryKey"), k => A(k, "Root") == "HKCR" && A(k, "Key") == ".foo");
    }

    [Fact]
    public void Environment_variables_add_to_path_at_the_end_or_the_start()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithPathEntry("[INSTALLDIR]bin").WithPathEntry("[INSTALLDIR]tools", prepend: true).WithEnvironmentVariable("APP_HOME", "[INSTALLDIR]"));

        var variables = wxs.All("Environment").ToDictionary(e => A(e, "Id")!);
        Assert.Equal(("PATH", "last", "set"), (A(variables["InstallKitEnv1"], "Name"), A(variables["InstallKitEnv1"], "Part"), A(variables["InstallKitEnv1"], "Action")));
        Assert.Equal("first", A(variables["InstallKitEnv2"], "Part"));
        Assert.Equal("all", A(variables["InstallKitEnv3"], "Part"));
        Assert.All(variables.Values, e => Assert.Equal("yes", A(e, "System")));
        Assert.All(variables.Values, e => Assert.NotEqual("yes", A(e, "Permanent")));
    }

    [Fact]
    public void A_per_user_install_writes_user_environment_variables()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithScope(InstallScope.PerUser).WithEnvironmentVariable("APP_HOME", "x"));

        Assert.Equal("no", A(wxs.One("Environment"), "System"));
    }

    [Fact]
    public void Firewall_exceptions_are_enabled_and_carry_their_port_program_and_scope()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App()
            .WithFirewallRule("Web", "8080")
            .WithFirewallRule("Service", executable: "Svc.exe", scope: FirewallScope.LocalSubnet, profile: FirewallProfile.Private));

        var rules = wxs.All("FirewallException").ToDictionary(r => A(r, "Name")!);
        // WixSharp leaves an exception disabled unless it is asked to enable it.
        Assert.All(rules.Values, r => Assert.Equal("yes", A(r, "Enabled")));
        Assert.Equal(("8080", "tcp", "any"), (A(rules["Web"], "Port"), A(rules["Web"], "Protocol"), A(rules["Web"], "Scope")));
        Assert.Equal("[INSTALLDIR]Svc.exe", A(rules["Service"], "Program"));
        Assert.Equal(("localSubnet", "private"), (A(rules["Service"], "Scope"), A(rules["Service"], "Profile")));
    }

    [Fact]
    public void Folder_permissions_land_on_the_install_folder_and_never_on_program_files()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App()
            .WithFolderPermission("Authenticated Users", FolderRights.Modify)
            .WithFolderPermission("Users", FolderRights.Modify, "logs")
            .WithFolderPermission(@"NT AUTHORITY\LocalService", FolderRights.Read, "bin"));

        var installDir = wxs.One("Directory", "INSTALLDIR");
        var inside = installDir.Descendants().Where(e => e.Name.LocalName == "PermissionEx").ToList();
        Assert.Equal(3, inside.Count);
        Assert.Equal(inside.Count, wxs.All("PermissionEx").Count());      // none anywhere else, in particular not on Program Files

        var modify = inside.Single(p => A(p, "User") == "Authenticated Users");
        Assert.Equal(new[] { "yes", "yes", "yes", "yes" }, new[] { A(modify, "GenericRead"), A(modify, "GenericWrite"), A(modify, "GenericExecute"), A(modify, "Delete") });
        Assert.Null(A(modify, "GenericAll"));
        Assert.Null(A(modify, "DeleteChild"));                           // Windows' "Modify" does not include it

        var service = inside.Single(p => A(p, "User") == "LocalService");
        Assert.Equal("NT AUTHORITY", A(service, "Domain"));
        Assert.Null(A(service, "GenericWrite"));

        // A folder with no files of the product is created, with its permission on it.
        var logs = installDir.Descendants().Single(e => e.Name.LocalName == "Directory" && A(e, "Name") == "logs");
        Assert.Equal("Users", A(logs.Descendants().Single(e => e.Name.LocalName == "PermissionEx"), "User"));
    }

    [Fact]
    public void Without_folder_permissions_there_is_no_permission_component_at_all()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App());

        Assert.Empty(wxs.All("PermissionEx"));
        Assert.Equal("INSTALLDIR", A(wxs.One("Directory", "INSTALLDIR"), "Id"));
    }

    [Fact]
    public void An_xml_value_edits_the_installed_file_with_escaped_brackets_in_the_xpath()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithXmlValue("app.config", "/configuration/appSettings/add[@key='Server']/@value", "[SERVER]"));

        var edit = wxs.One("XmlFile");
        Assert.Equal("setValue", A(edit, "Action"));
        Assert.Equal("/configuration/appSettings/add[\\[]@key='Server'[\\]]/@value", A(edit, "ElementPath"));
        Assert.Equal("[SERVER]", A(edit, "Value"));

        // It is a child of the component that holds the file, and refers to that file.
        var file = edit.Parent!.Elements().Single(e => e.Name.LocalName == "File");
        Assert.EndsWith("app.config", A(file, "Source"));
        Assert.Equal($"[#{A(file, "Id")}]", A(edit, "File"));
    }

    [Fact]
    public void Closing_a_program_asks_and_can_end_it()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithCloseApplication("App.exe", terminate: true, timeoutSeconds: 10).WithCloseApplication("Other.exe"));

        var rules = wxs.All("CloseApplication").ToDictionary(r => A(r, "Target")!);
        Assert.Equal(("0", "10"), (A(rules["App.exe"], "TerminateProcess"), A(rules["App.exe"], "Timeout")));
        Assert.Null(A(rules["Other.exe"], "TerminateProcess"));
        Assert.All(rules.Values, r => Assert.Equal("yes", A(r, "CloseMessage")));
    }

    [Fact]
    public void Optional_features_hold_their_files_and_the_rest_stays_in_complete()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App()
            .WithFeature("Documentation", "Help files")
            .WithFeature("Samples", installedByDefault: false)
            .WithDirectory(workspace.Docs, "docs", "Documentation")
            .WithDirectory(workspace.Docs, "samples", "Samples"));

        var features = wxs.All("Feature").ToDictionary(f => A(f, "Id")!);
        // WixSharp numbers the default feature when it has built several projects in one process ("Complete.1").
        var complete = features.Keys.Single(k => k.StartsWith("Complete"));
        Assert.Equal(new[] { "Documentation", "Samples" }, features.Keys.Where(k => k != complete).OrderBy(k => k));
        Assert.Equal(("Documentation", "1", "Help files"), (A(features["Documentation"], "Title"), A(features["Documentation"], "Level"), A(features["Documentation"], "Description")));
        Assert.Equal("2", A(features["Samples"], "Level"));                  // off by default

        System.Collections.Generic.List<string> FilesOf(string feature) =>
            features[feature].Elements().Where(e => e.Name.LocalName == "ComponentRef")
                .SelectMany(r => wxs.All("Component").Where(c => A(c, "Id") == A(r, "Id")))
                .SelectMany(c => c.Elements().Where(e => e.Name.LocalName == "File")).Select(f => A(f, "Source")!).ToList();

        Assert.Contains(FilesOf("Documentation"), f => f.EndsWith("readme.txt"));
        Assert.Contains(FilesOf("Samples"), f => f.EndsWith("readme.txt"));
        Assert.Contains(FilesOf(complete), f => f.EndsWith("App.exe"));
        Assert.DoesNotContain(FilesOf(complete), f => f.EndsWith("readme.txt"));
    }

    [Fact]
    public void A_prompted_property_is_secure_and_a_missing_default_leaves_out_the_value()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithPromptedProperty("SERVER", "Server", "localhost").WithPromptedProperty("NOTES", "Notes").WithProperty("MODE", "client"));

        var properties = wxs.All("Property").Where(p => new[] { "SERVER", "NOTES", "MODE" }.Contains(A(p, "Id"))).ToDictionary(p => A(p, "Id")!);
        Assert.Equal(("localhost", "yes"), (A(properties["SERVER"], "Value"), A(properties["SERVER"], "Secure")));
        Assert.Equal("yes", A(properties["NOTES"], "Secure"));
        Assert.Null(A(properties["NOTES"], "Value"));                        // WiX rejects an empty Value
        Assert.Null(A(properties["MODE"], "Secure"));
    }

    [Fact]
    public void A_service_is_installed_next_to_its_executable_and_stopped_and_removed_with_it()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithService("Svc.exe", "MySvc", s => s.WithDisplayName("My Service")));

        var component = wxs.One("ServiceInstall").Parent!;
        Assert.EndsWith("Svc.exe", A(component.Elements().Single(e => e.Name.LocalName == "File"), "Source"));
        Assert.Equal(("MySvc", "My Service", "auto"), (A(wxs.One("ServiceInstall"), "Name"), A(wxs.One("ServiceInstall"), "DisplayName"), A(wxs.One("ServiceInstall"), "Start")));
        Assert.Equal(3, component.Elements().Count(e => e.Name.LocalName == "ServiceControl"));
    }

    [Fact]
    public void A_side_by_side_product_has_the_name_code_folder_and_service_of_its_line()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithSideBySide(SideBySideMode.MajorMinor).WithService("Svc.exe", "MySvc"));

        var package = wxs.One("Package");
        Assert.Equal("My App 3.1", A(package, "Name"));
        Assert.Equal(SideBySide.DeriveUpgradeCode(System.Guid.Parse(Wxs.Code), "3.1"), System.Guid.Parse(A(package, "UpgradeCode")!));
        Assert.Equal("My App 3.1", A(wxs.One("Directory", "INSTALLDIR"), "Name"));
        Assert.Equal("MySvc-3.1", A(wxs.One("ServiceInstall"), "Name"));
    }

    [Fact]
    public void A_per_user_install_has_the_per_user_scope()
    {
        var workspace = new Wxs.Workspace();

        var wxs = Wxs.Msi(workspace, workspace.App().WithScope(InstallScope.PerUser));

        Assert.Equal("perUser", A(wxs.One("Package"), "Scope"));
    }

    [Fact]
    public void Files_with_long_names_get_identifiers_within_the_72_character_limit_and_every_reference_resolves()
    {
        var workspace = new Wxs.Workspace();
        var longName = "Microsoft.Extensions.Configuration.EnvironmentVariables.Extra.Long.Name.dll";
        System.IO.File.WriteAllText(System.IO.Path.Combine(workspace.Payload, longName), "x");
        var longConfig = "A.Very.Long.Application.Configuration.File.Name.For.Testing.Limits.config";
        System.IO.File.WriteAllText(System.IO.Path.Combine(workspace.Payload, longConfig), "<configuration/>");

        var wxs = Wxs.Msi(workspace, workspace.App().WithXmlValue(longConfig, "/configuration/@x", "1"));

        Assert.All(wxs.All("Component").Concat(wxs.All("File")), e => Assert.True(A(e, "Id")!.Length <= 72, A(e, "Id")));

        var components = wxs.All("Component").Select(c => A(c, "Id")).ToHashSet();
        Assert.All(wxs.All("ComponentRef"), r => Assert.Contains(A(r, "Id"), components));

        // The XML edit still points at the file it edits, whose id was shortened too.
        var edit = wxs.One("XmlFile");
        var file = edit.Parent!.Elements().Single(e => e.Name.LocalName == "File");
        Assert.EndsWith(longConfig, A(file, "Source"));
        Assert.Equal($"[#{A(file, "Id")}]", A(edit, "File"));
    }

    [Fact]
    public void A_service_in_a_subfolder_of_the_install_directory_is_installed_there()
    {
        var workspace = new Wxs.Workspace();
        var tools = System.IO.Path.Combine(workspace.Root, "tools");
        System.IO.Directory.CreateDirectory(tools);
        System.IO.File.WriteAllText(System.IO.Path.Combine(tools, "Tool.exe"), "x");
        System.IO.File.WriteAllText(System.IO.Path.Combine(tools, "other.txt"), "x");

        var wxs = Wxs.Msi(workspace, workspace.App().WithDirectory(tools, "tools").WithService(@"tools\Tool.exe", "ToolSvc"));

        var folder = wxs.One("Directory", "INSTALLDIR").Elements().Single(e => e.Name.LocalName == "Directory" && A(e, "Name") == "tools");
        var component = wxs.One("ServiceInstall").Parent!;
        Assert.Same(folder, component.Parent);
        Assert.EndsWith("Tool.exe", A(component.Elements().Single(e => e.Name.LocalName == "File"), "Source"));
        // The other file of the folder is still installed, once.
        Assert.Single(folder.Descendants(), e => e.Name.LocalName == "File" && (A(e, "Source") ?? "").EndsWith("other.txt"));
    }

    [Fact]
    public void A_service_and_an_xml_edit_in_a_feature_belong_to_that_feature_and_not_to_the_core()
    {
        var workspace = new Wxs.Workspace();
        var worker = System.IO.Path.Combine(workspace.Root, "worker");
        System.IO.Directory.CreateDirectory(worker);
        System.IO.File.WriteAllText(System.IO.Path.Combine(worker, "Worker.exe"), "x");
        System.IO.File.WriteAllText(System.IO.Path.Combine(worker, "worker.config"), "<configuration/>");

        var wxs = Wxs.Msi(workspace, workspace.App()
            .WithFeature("Worker", installedByDefault: false)
            .WithDirectory(worker, "worker", "Worker")
            .WithService(@"worker\Worker.exe", "WorkerSvc")
            .WithXmlValue(@"worker\worker.config", "/configuration/@x", "1"));

        var feature = wxs.All("Feature").Single(f => A(f, "Id") == "Worker");
        var refs = feature.Elements().Where(e => e.Name.LocalName == "ComponentRef").Select(r => A(r, "Id")).ToHashSet();

        // The service's and the XML edit's components are in the feature (which is off by default) ...
        Assert.Contains(A(wxs.One("ServiceInstall").Parent!, "Id"), refs);
        Assert.Contains(A(wxs.One("XmlFile").Parent!, "Id"), refs);

        // ... and in no other feature, in particular not in the always-installed one.
        var elsewhere = wxs.All("Feature").Where(f => A(f, "Id") != "Worker")
            .SelectMany(f => f.Elements().Where(e => e.Name.LocalName == "ComponentRef")).Select(r => A(r, "Id")).ToHashSet();
        Assert.DoesNotContain(A(wxs.One("ServiceInstall").Parent!, "Id"), elsewhere);
        Assert.DoesNotContain(A(wxs.One("XmlFile").Parent!, "Id"), elsewhere);
    }
}
