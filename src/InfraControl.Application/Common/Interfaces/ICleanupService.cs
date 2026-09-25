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
    public int BatchSize { get; set; } = 500;

    public DateTime OutboxCutoff(DateTime utcNow) => utcNow.AddDays(-OutboxProcessedDays);
    public DateTime ProcessedCutoff(DateTime utcNow) => utcNow.AddDays(-ProcessedMessagesDays);
    public DateTime AuditCutoff(DateTime utcNow) => utcNow.AddDays(-AuditEntriesDays);
    public DateTime MonitoringCutoff(DateTime utcNow) => utcNow.AddDays(-MonitoringResultsDays);
    public DateTime NotificationsCutoff(DateTime utcNow) => utcNow.AddDays(-NotificationsDays);
    public DateTime RemoteSessionsCutoff(DateTime utcNow) => utcNow.AddDays(-RemoteSessionsDays);
    public DateTime DiscoveryCutoff(DateTime utcNow) => utcNow.AddDays(-DiscoveryRunsDays);
}

public sealed record CleanupResult(
    int OutboxDeleted,
    int ProcessedDeleted,
    int AuditDeleted,
    int MonitoringDeleted,
    int NotificationDeleted,
    int RemoteSessionDeleted,
    int DiscoveryDeleted)
{
    public int TotalDeleted => OutboxDeleted + ProcessedDeleted + AuditDeleted
        + MonitoringDeleted + NotificationDeleted + RemoteSessionDeleted + DiscoveryDeleted;
}

public interface ICleanupService
{
    Task<CleanupResult> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
