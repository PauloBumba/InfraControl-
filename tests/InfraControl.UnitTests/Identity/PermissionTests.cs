using FluentAssertions;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Identity;

public sealed class PermissionTests
{
    [Fact]
    public void Viewer_ShouldReceiveOnlyReadPermissions()
    {
        var permissions = RolePermissions.ForRole(UserRole.Viewer);

        permissions.Should().BeEquivalentTo([
            Permission.DashboardView,
            Permission.InventoryRead,
            Permission.MonitoringRead,
            Permission.IncidentsRead,
            Permission.TasksRead,
            Permission.RemoteSessionsRead
        ]);
        permissions.Should().NotContain(Permission.InventoryManage);
        permissions.Should().NotContain(Permission.IdentityRolesManage);
        permissions.Should().NotContain(Permission.RemoteSshConnect);
        permissions.Should().NotContain(Permission.RemoteRdpConnect);
        permissions.Should().NotContain(Permission.CredentialUse);
        permissions.Should().NotContain(Permission.CredentialManage);
        permissions.Should().NotContain(Permission.RetentionManage);
    }

    [Fact]
    public void User_ShouldInheritViewerPermissionsAndManagementPermissions()
    {
        var permissions = RolePermissions.ForRole(UserRole.User);

        permissions.Should().Contain(Permission.DashboardView);
        permissions.Should().Contain(Permission.InventoryRead);
        permissions.Should().Contain(Permission.InventoryManage);
        permissions.Should().Contain(Permission.TasksManage);
        permissions.Should().NotContain(Permission.IdentityRolesManage);
        permissions.Should().Contain(Permission.RemoteSessionsRead);
        permissions.Should().Contain(Permission.RemoteSshConnect);
        permissions.Should().Contain(Permission.RemoteRdpConnect);
        permissions.Should().NotContain(Permission.CredentialUse);
        permissions.Should().NotContain(Permission.CredentialManage);
        permissions.Should().NotContain(Permission.RetentionManage);
    }

    [Fact]
    public void Admin_ShouldReceiveEveryPermission()
    {
        var permissions = RolePermissions.ForRole(UserRole.Admin);

        permissions.Should().BeEquivalentTo(Enum.GetValues<Permission>());
    }

    [Fact]
    public void MultipleRoles_ShouldProduceUnionWithoutDuplicates()
    {
        var permissions = RolePermissions.ForRoles([UserRole.Viewer, UserRole.User]);

        permissions.Should().HaveSameCount(RolePermissions.ForRole(UserRole.User));
        permissions.Should().Contain(Permission.IdentityUsersRead);
    }
}
