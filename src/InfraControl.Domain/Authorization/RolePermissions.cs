using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Authorization;

public static class RolePermissions
{
    private static readonly IReadOnlySet<Permission> ViewerPermissions = new HashSet<Permission>
    {
        Permission.DashboardView,
        Permission.InventoryRead,
        Permission.MonitoringRead,
        Permission.IncidentsRead,
        Permission.TasksRead,
        Permission.SlaRead,
        Permission.RemoteSessionsRead
    };

    private static readonly IReadOnlySet<Permission> UserPermissions = new HashSet<Permission>(ViewerPermissions)
    {
        Permission.IdentityUsersRead,
        Permission.InventoryManage,
        Permission.MonitoringManage,
        Permission.IncidentsManage,
        Permission.TasksManage,
        Permission.SlaManage,
        Permission.RemoteSshConnect,
        Permission.RemoteRdpConnect
    };

    private static readonly IReadOnlySet<Permission> AdminPermissions =
        Enum.GetValues<Permission>().ToHashSet();

    public static IReadOnlySet<Permission> ForRole(UserRole role) => role switch
    {
        UserRole.Admin => AdminPermissions,
        UserRole.User => UserPermissions,
        UserRole.Viewer => ViewerPermissions,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown user role.")
    };

    public static IReadOnlySet<Permission> ForRoles(IEnumerable<UserRole> roles)
        => roles.SelectMany(ForRole).ToHashSet();
}
