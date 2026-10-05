namespace InfraControl.Application.Common.Interfaces;

public sealed record AuditEntrySummary(
    Guid Id,
    string Action,
    string Resource,
    string Outcome,
    string? ActorId,
    string? ActorName,
    string? CorrelationId,
    string? Error,
    DateTime OccurredAt);

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public interface IAuditReader
{
    Task<PagedResult<AuditEntrySummary>> SearchAsync(
        string? action,
        string? outcome,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
