using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SwitchPortRepository(AppDbContext dbContext) : ISwitchPortRepository
{
    public async Task AddAsync(SwitchPort port, CancellationToken cancellationToken = default)
    {
        await dbContext.SwitchPorts.AddAsync(port, cancellationToken);
    }

    public Task<SwitchPort?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.SwitchPorts
            .SingleOrDefaultAsync(port => port.Id == id, cancellationToken);

    public void Remove(SwitchPort port)
        => dbContext.SwitchPorts.Remove(port);

    public Task<SwitchPort?> GetByNameAsync(Guid deviceId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.SwitchPorts
            .FirstOrDefaultAsync(
                port => port.DeviceId == deviceId && port.Name.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SwitchPort>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToArray();
        return await dbContext.SwitchPorts
            .AsNoTracking()
            .Where(port => idList.Contains(port.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SwitchPort>> SearchAsync(
        Guid deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SwitchPorts.AsNoTracking()
            .Where(port => port.DeviceId == deviceId);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(port =>
                port.Name.ToLower().Contains(normalized)
                || (port.Description != null && port.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(port => port.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SwitchPort>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
