namespace InfraControl.Application.Common.Interfaces;

public sealed record RetentionOptions
{
    public int OutboxProcessedDays { get; set; } = 30;
    public int ProcessedMessagesDays { get; set; } = 30;
    public int AuditEntriesDays { get; set; } = 90;
    public int MonitoringResultsDays { get; set; } = 180;
    public int NotificationsDays { get; set; } = 90;
    public int RemoteSessionsDays { get; set; } = 90;
    public int DiscoveryRunsDays { get; set; } = 180;
    public int MonitoringEventsDays { get; set; } = 90;
    public int InvitationsDays { get; set; } = 90;
    public int BatchSize { get; set; } = 500;

    public DateTime OutboxCutoff(DateTime utcNow) => utcNow.AddDays(-OutboxProcessedDays);
    public DateTime ProcessedCutoff(DateTime utcNow) => utcNow.AddDays(-ProcessedMessagesDays);
    public DateTime AuditCutoff(DateTime utcNow) => utcNow.AddDays(-AuditEntriesDays);
    public DateTime MonitoringCutoff(DateTime utcNow) => utcNow.AddDays(-MonitoringResultsDays);
    public DateTime NotificationsCutoff(DateTime utcNow) => utcNow.AddDays(-NotificationsDays);
    public DateTime RemoteSessionsCutoff(DateTime utcNow) => utcNow.AddDays(-RemoteSessionsDays);
    public DateTime DiscoveryCutoff(DateTime utcNow) => utcNow.AddDays(-DiscoveryRunsDays);
    public DateTime MonitoringEventsCutoff(DateTime utcNow) => utcNow.AddDays(-MonitoringEventsDays);
    public DateTime InvitationsCutoff(DateTime utcNow) => utcNow.AddDays(-InvitationsDays);
}

/// <summary>
/// Medição de performance dos requests (Dia 5 — docs/20): acima de
/// <c>SlowRequestMs</c> o PerformanceBehavior loga Warning.
/// </summary>
public sealed record PerformanceOptions
{
    public int SlowRequestMs { get; set; } = 1000;
}

public sealed record CleanupResult(
    int OutboxDeleted,
    int ProcessedDeleted,
    int AuditDeleted,
    int MonitoringDeleted,
    int NotificationDeleted,
    int RemoteSessionDeleted,
    int DiscoveryDeleted,
    int MonitoringEventsDeleted = 0,
    int InvitationsDeleted = 0)
{
    public int TotalDeleted => OutboxDeleted + ProcessedDeleted + AuditDeleted
        + MonitoringDeleted + NotificationDeleted + RemoteSessionDeleted + DiscoveryDeleted
        + MonitoringEventsDeleted + InvitationsDeleted;
}

public interface ICleanupService
{
    Task<CleanupResult> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
