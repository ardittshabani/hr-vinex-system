using Vinex.HR.Contracts.Security;

namespace Vinex.HR.Tests.Unit;

public class PermissionsTests
{
    [Fact]
    public void Permission_names_are_unique()
    {
        Permissions.All.Distinct().Count().ShouldBe(Permissions.All.Count);
    }

    [Fact]
    public void HrAdministrator_has_every_permission()
    {
        foreach (var permission in Permissions.All)
        {
            RolePermissions.Has(RoleNames.HRAdministrator, permission).ShouldBeTrue(permission);
        }
    }

    [Fact]
    public void Management_is_read_only_and_cannot_see_sensitive_data_or_admin_areas()
    {
        var management = RolePermissions.For(RoleNames.Management);
        management.ShouldContain(Permissions.DashboardView);
        management.ShouldContain(Permissions.ReportsView);
        management.ShouldNotContain(Permissions.EmployeesViewSensitive);
        management.ShouldNotContain(Permissions.UsersManage);
        management.ShouldNotContain(Permissions.SettingsManage);
        management.ShouldNotContain(Permissions.AuditView);
        management.Where(p => p.EndsWith(".Manage")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void Unknown_roles_have_no_permissions(string? role)
    {
        RolePermissions.For(role).ShouldBeEmpty();
    }
}
