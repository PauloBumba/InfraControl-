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

    /// <summary>Ler serviços/SLA/SLO (módulo Fase 8 — F8-T001→T007).</summary>
    SlaRead,

    /// <summary>Gerenciar serviços/SLA/SLO (módulo Fase 8).</summary>
    SlaManage,
    AuditRead,
    RemoteSessionsRead,

    /// <summary>Abrir/encerrar sessão SSH em devices (= device.ssh.connect, docs/10).</summary>
    RemoteSshConnect,

    /// <summary>Abrir/encerrar sessão RDP em devices (= device.rdp.connect, docs/10).</summary>
    RemoteRdpConnect,

    /// <summary>Usar credencial armazenada para abrir sessão (= credential.use, docs/10).</summary>
    CredentialUse,

    /// <summary>Gerenciar credenciais de acesso remoto (= credential.manage, docs/10; consumo em F11-T006).</summary>
    CredentialManage,

    /// <summary>Gerenciar políticas de retenção por tipo de dado (= operations.retain.manage, docs/11; consumo em F12-T002).</summary>
    RetentionManage
}
