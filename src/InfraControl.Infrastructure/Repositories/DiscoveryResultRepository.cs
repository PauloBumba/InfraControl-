using InfraControl.BuildingBlocks.Domain;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class DiscoveryResultRepository(AppDbContext dbContext) : IDiscoveryResultRepository
{
    public async Task AddAsync(DiscoveryResult result, CancellationToken cancellationToken = default)
    {
        await dbContext.DiscoveryResults.AddAsync(result, cancellationToken);
    }

    public Task<DiscoveryResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.DiscoveryResults
            .SingleOrDefaultAsync(result => result.Id == id, cancellationToken);

    public void Remove(DiscoveryResult result)
        => dbContext.DiscoveryResults.Remove(result);

    public async Task<PagedResult<DiscoveryResult>> SearchByRunAsync(
        Guid discoveryRunId,
        DiscoveryResultKind? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // F18-T002: `kind` opcional — filtrado ANTES do COUNT, então o
        // TotalCount do PagedResult respeita o filtro.
        var query = dbContext.DiscoveryResults
            .AsNoTracking()
            .Where(result => result.DiscoveryRunId == discoveryRunId &&
                (kind == null || result.Kind == kind));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(result => result.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DiscoveryResult>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<DiscoveryResult>> ListByKindsAsync(
        IReadOnlyCollection<DiscoveryResultKind> kinds,
        CancellationToken cancellationToken = default)
    {
        // F18-T008: filtro de kind traduzido para SQL (coluna enum-string);
        // tudo o mais (dedupe por MAC, merge de campos) é feito no handler.
        return await dbContext.DiscoveryResults
            .AsNoTracking()
            .Where(result => kinds.Contains(result.Kind))
            .OrderByDescending(result => result.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
