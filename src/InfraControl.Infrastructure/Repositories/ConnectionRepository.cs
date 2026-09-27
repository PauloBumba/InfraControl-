using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class ConnectionRepository(AppDbContext dbContext)
    : IConnectionRepository
{
    // Tracking normal (padrão): o handler de delete usa o retorno direto
    // no Remove; reads puros (pair/endpoints/search) são AsNoTracking.
    public Task<Connection?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.Connections
            .FirstOrDefaultAsync(connection => connection.Id == id, cancellationToken);

    public async Task AddAsync(
        Connection entity, CancellationToken cancellationToken = default)
        => await dbContext.Connections.AddAsync(entity, cancellationToken);

    public void Remove(Connection entity)
        => dbContext.Connections.Remove(entity);

    public Task<Connection?> GetPairAsync(
        Guid sourceInterfaceId,
        Guid targetInterfaceId,
        CancellationToken cancellationToken = default)
        => dbContext.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(connection =>
                connection.SourceInterfaceId == sourceInterfaceId
                && connection.TargetInterfaceId == targetInterfaceId,
                cancellationToken);

    public async Task<ConnectionWithEndpoints?> GetWithEndpointsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var query =
            from connection in dbContext.Connections.AsNoTracking()
            where connection.Id == id
            from source in dbContext.NetworkInterfaces.AsNoTracking()
                .Where(nic => nic.Id == connection.SourceInterfaceId)
            from target in dbContext.NetworkInterfaces.AsNoTracking()
                .Where(nic => nic.Id == connection.TargetInterfaceId)
            select new ConnectionWithEndpoints(connection, source, target);

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasConnectionsForInterfaceAsync(
        Guid interfaceId, CancellationToken cancellationToken = default)
        => dbContext.Connections.AnyAsync(connection =>
            connection.SourceInterfaceId == interfaceId
            || connection.TargetInterfaceId == interfaceId,
            cancellationToken);

    public async Task<PagedResult<ConnectionWithEndpoints>> SearchAsync(
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from connection in dbContext.Connections.AsNoTracking()
            from source in dbContext.NetworkInterfaces.AsNoTracking()
                .Where(nic => nic.Id == connection.SourceInterfaceId)
            from target in dbContext.NetworkInterfaces.AsNoTracking()
                .Where(nic => nic.Id == connection.TargetInterfaceId)
            select new { connection, source, target };

        if (deviceId.HasValue)
            query = query.Where(item =>
                item.source.DeviceId == deviceId.Value
                || item.target.DeviceId == deviceId.Value);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.source.Name.ToLower().Contains(normalized)
                || item.target.Name.ToLower().Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.source.Name)
            .ThenBy(item => item.target.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ConnectionWithEndpoints(
                item.connection, item.source, item.target))
            .ToListAsync(cancellationToken);

        return new PagedResult<ConnectionWithEndpoints>(items, totalCount, page, pageSize);
    }

    // F6-T005: vizinhos em1 hop — grafo não-dirigido (qualquer lado da
    // aresta), si-mesmo excluído.
    public async Task<IReadOnlyList<Guid>> GetNeighborDeviceIdsAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var edges = await dbContext.Connections.AsNoTracking()
            .Join(dbContext.NetworkInterfaces.AsNoTracking(),
                connection => connection.SourceInterfaceId,
                nic => nic.Id,
                (connection, nic) => new { nic.DeviceId, connection.TargetInterfaceId })
            .Join(dbContext.NetworkInterfaces.AsNoTracking(),
                pair => pair.TargetInterfaceId,
                nic => nic.Id,
                (pair, target) => new { From = pair.DeviceId, To = target.DeviceId })
            .Where(edge => edge.From == deviceId || edge.To == deviceId)
            .ToListAsync(cancellationToken);

        return edges
            .SelectMany(edge => new[] { edge.From, edge.To })
            .Where(id => id != deviceId)
            .Distinct()
            .ToList();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
