namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos de dado cobertos pela retenção (F12-T002 + F50). Os 10 primeiros
/// são aplicados pelo <c>CleanupService</c> (docs/11); <c>Backups</c>
/// poda os arquivos de dump (BackupWorker); Grafana/Zabbix/Databases são
/// <b>política gerenciada</b> — registrada na tela, sem aplicação pelo
/// app (sistemas externos/bancos da stack não são podados por aqui).
/// </summary>
public enum RetentionDataType
{
    OutboxMessages,
    ProcessedMessages,
    AuditEntries,
    MonitoringResults,
    Notifications,
    RemoteSessions,
    DiscoveryRuns,
    MonitoringEvents,
    Invitations,
    UniFiSyncRuns,
    /// <summary>Histórico append-only de transições de ocorrência (F55 —
    /// cresce a cada evento; a <see cref="Occurrence"/> atual é estado e
    /// não é podada).</summary>
    OccurrenceTransitions,
    Backups,
    Grafana,
    Zabbix,
    Databases
}
