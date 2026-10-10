using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F27 Fase 3.1: hosts do Zabbix sincronizados no banco.
/// </summary>
public sealed class ZabbixHostsRepository(AppDbContext dbContext)
    : IZabbixHostsRepository
{
    public async Task<ZabbixHost?> GetByHostIdAsync(
        string hostId, CancellationToken cancellationToken)
        => await dbContext.ZabbixHosts
            .FirstOrDefaultAsync(
                h => h.HostId == hostId, cancellationToken);

    public async Task<IReadOnlyList<ZabbixHost>> ListAsync(
        CancellationToken cancellationToken)
        => await dbContext.ZabbixHosts
            .AsNoTracking()
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<ZabbixHost>> SearchAsync(
        string? term,
        bool? enabled,
        bool? missing,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ZabbixHosts.AsNoTracking().AsQueryable();

        if (enabled.HasValue)
            query = query.Where(h => h.Enabled == enabled.Value);

        if (missing.HasValue)
            query = missing.Value
                ? query.Where(h => h.MissingSinceUtc != null)
                : query.Where(h => h.MissingSinceUtc == null);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(h =>
                h.Host.ToLower().Contains(normalized) ||
                h.Name.ToLower().Contains(normalized) ||
                (h.Ip != null && h.Ip.ToLower().Contains(normalized)) ||
                h.HostId.ToLower().Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(h => h.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ZabbixHost>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(ZabbixHost host, CancellationToken cancellationToken)
        => await dbContext.ZabbixHosts.AddAsync(host, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken);
}
