namespace InfraControl.Application.Common.Interfaces;

public sealed record RetentionOptions
{
    public int OutboxProcessedDays { get; set; } = 30;
    public int ProcessedMessagesDays { get; set; } = 30;
    public int AuditEntriesDays { get; set; } = 90;
    public int BatchSize { get; set; } = 500;

    public DateTime OutboxCutoff(DateTime utcNow) => utcNow.AddDays(-OutboxProcessedDays);
    public DateTime ProcessedCutoff(DateTime utcNow) => utcNow.AddDays(-ProcessedMessagesDays);
    public DateTime AuditCutoff(DateTime utcNow) => utcNow.AddDays(-AuditEntriesDays);
}

public sealed record CleanupResult(int OutboxDeleted, int ProcessedDeleted, int AuditDeleted)
{
    public int TotalDeleted => OutboxDeleted + ProcessedDeleted + AuditDeleted;
}

public interface ICleanupService
{
    Task<CleanupResult> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
