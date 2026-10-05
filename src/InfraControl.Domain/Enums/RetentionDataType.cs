namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos de dado cobertos pela retenção/cleanup (F12-T002) — os mesmos 9
/// que o <c>CleanupService</c> apaga (docs/11).
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
    Invitations
}
