using System.IO;
using System.Linq;
using Xunit;

namespace InstallKit.Tests;

public class FolderPermissionTests
{
    static (InstallerPlan Plan, MsiBuilder Builder) NewPlan()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "App.exe"), "");
        var config = Directory.CreateDirectory(Path.Combine(dir, "cfg")).FullName;
        File.WriteAllText(Path.Combine(config, "a.json"), "");
        var plan = new InstallerPlan(InstallerContext.FromEnvironment(new[] { "--payload", $"App={dir}", "--output", dir }));
        var builder = MsiBuilder.Create("App").WithUpgradeCode("2f0a4e2c-1111-4c1e-9b1a-000000000001").Content(c => c.Payload("App").Directory(config, "config"));
        return (plan, builder);
    }

    static string[] Codes(InstallerPlan plan) => InstallerValidator.Validate(plan).Select(e => e.Code).ToArray();

    [Fact]
    public void Builder_collects_permissions_for_the_install_dir_and_subfolders()
    {
        var msi = MsiBuilder.Create("App")
            .Machine(m => m
                .FolderPermission("Authenticated Users", FolderRights.Modify)
                .FolderPermission("Users", FolderRights.Read, "data/logs/"))
            .Build();

        Assert.Null(msi.FolderPermissions[0].Folder);
        Assert.Equal(FolderRights.Modify, msi.FolderPermissions[0].Rights);
        Assert.Equal(@"data\logs", msi.FolderPermissions[1].Folder);
        Assert.Single(new MsiDefinition("App").AddFolderPermission("Users", FolderRights.FullControl, "x").FolderPermissions);
    }

    [Theory]
    [InlineData("Users", null, "Users")]
    [InlineData(@"NT AUTHORITY\LocalService", "NT AUTHORITY", "LocalService")]
    [InlineData(@"NT SERVICE\My Service", "NT SERVICE", "My Service")]
    public void The_account_is_split_into_domain_and_name(string account, string? domain, string user)
    {
        var permission = new FolderPermission(account, FolderRights.Read);

        Assert.Equal(domain, permission.Domain);
        Assert.Equal(user, permission.UserName);
    }

    [Fact]
    public void Valid_permissions_pass()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder
            .Machine(m => m
                .FolderPermission("Authenticated Users", FolderRights.Modify)
                .FolderPermission("Users", FolderRights.Modify, "logs")
                .FolderPermission(@"NT AUTHORITY\LocalService", FolderRights.Read, "config")));

        Assert.Empty(Codes(plan));
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"NT AUTHORITY\")]
    public void An_account_is_required(string account)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FolderPermission(account, FolderRights.Read)));

        Assert.Contains("IK250", Codes(plan));
    }

    [Theory]
    [InlineData(@"..\other")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"a\*")]
    [InlineData(@"a\.\b")]
    public void A_folder_must_stay_below_the_install_directory(string folder)
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FolderPermission("Users", FolderRights.Read, folder)));

        Assert.Contains("IK251", Codes(plan));
    }

    [Fact]
    public void A_folder_inside_a_payload_folder_is_rejected_but_the_payload_folder_itself_is_fine()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FolderPermission("Users", FolderRights.Read, @"config\secrets")));

        var error = InstallerValidator.Validate(plan).Single();
        Assert.Equal("IK252", error.Code);
        Assert.Contains("payload folder", error.Message);
    }

    [Fact]
    public void The_same_account_and_folder_twice_is_reported()
    {
        var (plan, builder) = NewPlan();
        plan.Msi(builder.Machine(m => m.FolderPermission("Users", FolderRights.Read, "logs").FolderPermission("USERS", FolderRights.Modify, "logs")));

        Assert.Contains("IK253", Codes(plan));
    }
}
