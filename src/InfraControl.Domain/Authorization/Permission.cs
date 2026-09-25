namespace InfraControl.Domain.Authorization;

/// <summary>
/// Permissões granulares estáveis usadas por casos de uso e autorização HTTP.
/// A enumeração pertence ao domínio; claims e policies são adaptadores técnicos.
/// </summary>
public enum Permission
{
    DashboardView,
    IdentityUsersRead,
    IdentityUsersManage,
    IdentityRolesManage,
    InventoryRead,
    InventoryManage,
    MonitoringRead,
    MonitoringManage,
    IncidentsRead,
    IncidentsManage,
    TasksRead,
    TasksManage,
    AuditRead,
    RemoteSessionsRead,

    /// <summary>Abrir/encerrar sessão SSH em devices (= device.ssh.connect, docs/10).</summary>
    RemoteSshConnect
}
