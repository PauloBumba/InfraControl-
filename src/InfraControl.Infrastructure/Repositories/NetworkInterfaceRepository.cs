using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class NetworkInterfaceRepository(AppDbContext dbContext) : INetworkInterfaceRepository
{
    public async Task AddAsync(NetworkInterface nic, CancellationToken cancellationToken = default)
    {
        await dbContext.NetworkInterfaces.AddAsync(nic, cancellationToken);
    }

    public Task<NetworkInterface?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.NetworkInterfaces
            .SingleOrDefaultAsync(nic => nic.Id == id, cancellationToken);

    public void Remove(NetworkInterface nic)
        => dbContext.NetworkInterfaces.Remove(nic);

    public Task<NetworkInterface?> GetByNameAsync(Guid deviceId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.NetworkInterfaces
            .FirstOrDefaultAsync(
                nic => nic.DeviceId == deviceId && nic.Name.ToLower() == normalized,
                cancellationToken);
    }

    public Task<bool> HasMacsAsync(Guid interfaceId, CancellationToken cancellationToken = default)
        => dbContext.MacAddresses.AnyAsync(mac => mac.InterfaceId == interfaceId, cancellationToken);

    public async Task<PagedResult<NetworkInterface>> SearchAsync(
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.NetworkInterfaces.AsNoTracking().AsQueryable();

        if (deviceId.HasValue)
            query = query.Where(nic => nic.DeviceId == deviceId.Value);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(nic =>
                nic.Name.ToLower().Contains(normalized)
                || (nic.Description != null && nic.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(nic => nic.DeviceId)
            .ThenBy(nic => nic.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NetworkInterface>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
