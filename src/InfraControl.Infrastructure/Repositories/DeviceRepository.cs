using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class DeviceRepository(AppDbContext dbContext) : IDeviceRepository
{
    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await dbContext.Devices.AddAsync(device, cancellationToken);
    }

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Devices
            .SingleOrDefaultAsync(device => device.Id == id, cancellationToken);

    public void Remove(Device device)
        => dbContext.Devices.Remove(device);

    public Task<Device?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Devices
            .FirstOrDefaultAsync(
                device => device.Name.ToLower() == normalized,
                cancellationToken);
    }

    public Task<Device?> GetByIpAsync(string ip, CancellationToken cancellationToken = default)
    {
        var normalized = ip.Trim().ToLowerInvariant();
        return dbContext.Devices
            .FirstOrDefaultAsync(
                device => device.Ip != null && device.Ip.ToLower() == normalized,
                cancellationToken);
    }

    public Task<Device?> GetByMacAsync(string mac, CancellationToken cancellationToken = default)
    {
        // Comparação sem separador nem caixa: cobre "aa:bb:..." e "AA-BB-...".
        var normalized = mac.Replace(":", string.Empty)
            .Replace("-", string.Empty).Trim().ToLowerInvariant();
        return dbContext.Devices
            .FirstOrDefaultAsync(
                device => device.Mac != null
                    && device.Mac.Replace(":", string.Empty).ToLower() == normalized,
                cancellationToken);
    }

    public Task<bool> HasByRackAsync(Guid rackId, CancellationToken cancellationToken = default)
        => dbContext.Devices.AnyAsync(device => device.RackId == rackId, cancellationToken);

    public Task<bool> HasInterfacesAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.NetworkInterfaces.AnyAsync(nic => nic.DeviceId == deviceId, cancellationToken);

    public Task<bool> HasPortsAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.SwitchPorts.AnyAsync(port => port.DeviceId == deviceId, cancellationToken);

    public Task<bool> HasTasksAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
        => dbContext.TaskDevices.AnyAsync(
            link => link.DeviceId == deviceId, cancellationToken);

    public async Task<IReadOnlyList<Device>> GetAllOrderedByNameAsync(CancellationToken cancellationToken = default)
        => await dbContext.Devices
            .AsNoTracking()
            .OrderBy(device => device.Name)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Device>> SearchAsync(
        Guid? locationId,
        Guid? rackId,
        DeviceType? type,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Devices.AsNoTracking().AsQueryable();

        if (locationId.HasValue)
            query = query.Where(device => device.LocationId == locationId.Value);

        if (rackId.HasValue)
            query = query.Where(device => device.RackId == rackId.Value);

        if (type.HasValue)
            query = query.Where(device => device.Type == type.Value);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(device =>
                device.Name.ToLower().Contains(normalized)
                || (device.Ip != null && device.Ip.ToLower().Contains(normalized))
                || (device.Serial != null && device.Serial.ToLower().Contains(normalized))
                || (device.Mac != null && device.Mac.ToLower().Contains(normalized))
                || (device.Vendor != null && device.Vendor.ToLower().Contains(normalized))
                || (device.Model != null && device.Model.ToLower().Contains(normalized))
                || (device.Description != null && device.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(device => device.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Device>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
