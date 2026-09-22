using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class AuditReader(AppDbContext dbContext) : IAuditReader
{
    public async Task<PagedResult<AuditEntrySummary>> SearchAsync(
        string? action,
        string? outcome,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(entry => entry.Action == action);

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(entry => entry.Outcome == outcome);

        if (from.HasValue)
            query = query.Where(entry => entry.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(entry => entry.OccurredAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AuditEntrySummary(
                entry.Id,
                entry.Action,
                entry.Resource,
                entry.Outcome,
                entry.ActorId,
                entry.CorrelationId,
                entry.Error,
                entry.OccurredAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntrySummary>(items, totalCount, page, pageSize);
    }
}
