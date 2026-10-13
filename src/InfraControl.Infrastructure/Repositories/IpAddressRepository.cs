using System.Net;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class IpAddressRepository(AppDbContext dbContext) : IIpAddressRepository
{
    public async Task AddAsync(IpAddress entity, CancellationToken cancellationToken = default)
    {
        await dbContext.IpAddresses.AddAsync(entity, cancellationToken);
    }

    public Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.IpAddresses
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public void Remove(IpAddress entity)
        => dbContext.IpAddresses.Remove(entity);

    public Task<IpAddress?> GetByIpAsync(string ip, CancellationToken cancellationToken = default)
    {
        var normalized = Canonical(ip);
        return dbContext.IpAddresses
            .FirstOrDefaultAsync(entity => entity.Ip == normalized, cancellationToken);
    }

    public async Task<PagedResult<IpAddress>> SearchAsync(
        Guid? interfaceId,
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.IpAddresses.AsNoTracking().AsQueryable();

        if (interfaceId.HasValue)
            query = query.Where(entity => entity.InterfaceId == interfaceId.Value);

        if (deviceId.HasValue)
        {
            var device = deviceId.Value;
            query = query.Where(entity => dbContext.NetworkInterfaces.Any(
                nic => nic.Id == entity.InterfaceId && nic.DeviceId == device));
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            // Armazenamento é canônico (minúsculo); termo igual — substring literal.
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(entity => entity.Ip.Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(entity => entity.Ip)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<IpAddress>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<IpInterfaceClaim>> ListInterfaceClaimsAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.IpAddresses
            .AsNoTracking()
            .Join(dbContext.NetworkInterfaces,
                address => address.InterfaceId,
                nic => nic.Id,
                (address, nic) => new IpInterfaceClaim(address.Ip, nic.DeviceId))
            .OrderBy(claim => claim.Ip)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    private static string Canonical(string value)
        => IPAddress.TryParse(value.Trim(), out var address)
            ? address.ToString()
            : value.Trim().ToLowerInvariant();
}
