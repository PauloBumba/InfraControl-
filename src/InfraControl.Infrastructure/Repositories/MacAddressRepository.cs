using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class MacAddressRepository(AppDbContext dbContext) : IMacAddressRepository
{
    public async Task AddAsync(MacAddress mac, CancellationToken cancellationToken = default)
    {
        await dbContext.MacAddresses.AddAsync(mac, cancellationToken);
    }

    public Task<MacAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.MacAddresses
            .SingleOrDefaultAsync(mac => mac.Id == id, cancellationToken);

    public void Remove(MacAddress mac)
        => dbContext.MacAddresses.Remove(mac);

    public Task<MacAddress?> GetByMacAsync(string mac, CancellationToken cancellationToken = default)
    {
        // Unicidade global: comparação ignora separadores e caixa —
        // armazenamento normalizado aa:bb:cc:dd:ee:ff, termo compactado em 12 hex.
        var compact = Compact(mac);
        return dbContext.MacAddresses
            .FirstOrDefaultAsync(entity => entity.Mac.Replace(":", "") == compact, cancellationToken);
    }

    public async Task<PagedResult<MacAddress>> SearchAsync(
        Guid? interfaceId,
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MacAddresses.AsNoTracking().AsQueryable();

        if (interfaceId.HasValue)
            query = query.Where(mac => mac.InterfaceId == interfaceId.Value);

        if (deviceId.HasValue)
        {
            var device = deviceId.Value;
            query = query.Where(mac => dbContext.NetworkInterfaces.Any(
                nic => nic.Id == mac.InterfaceId && nic.DeviceId == device));
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = Compact(term);
            query = query.Where(mac => mac.Mac.Replace(":", "").Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(mac => mac.Mac)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MacAddress>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    private static string Compact(string value)
        => value.Trim()
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();
}
