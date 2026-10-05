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
                // Fase D: `ActorId` grava o e-mail do Identity (AuditBehavior) ou o
                // `DomainUsers.Email` (SecurityAudit) — resolvido aqui em DisplayName,
                // com case-insensitive (login pode gravar caixa mista; `User.Email` é
                // normalizado em minúsculas). Sem correspondência (usuário removido) →
                // null e o front exibe o valor gravado, como antes.
                entry.ActorId == null
                    ? null
                    : dbContext.DomainUsers
                        .AsNoTracking()
                        .Where(user => user.Email == entry.ActorId.ToLower())
                        .Select(user => user.DisplayName)
                        .FirstOrDefault(),
                entry.CorrelationId,
                entry.Error,
                entry.OccurredAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntrySummary>(items, totalCount, page, pageSize);
    }
}
