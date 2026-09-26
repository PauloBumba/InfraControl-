using InfraControl.Application.Network.Topology.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F3-T006: grafo topológico — interface⋈device (nós) + conexões (arestas)
/// + último Ping por device (status herdado pelo node).
/// </summary>
public sealed class TopologyRepository(AppDbContext dbContext)
    : ITopologyRepository
{
    public async Task<TopologyGraph> GetGraphAsync(
        CancellationToken cancellationToken = default)
    {
        var nodes = await (
            from nic in dbContext.NetworkInterfaces.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on nic.DeviceId equals device.Id
            orderby device.Name, nic.Name
            select new
            {
                nic.Id,
                nic.DeviceId,
                nic.Name,
                DeviceName = device.Name,
                DeviceType = device.Type,
                device.Ip
            }).ToListAsync(cancellationToken);

        if (nodes.Count == 0)
            return new TopologyGraph([], []);

        // Último Ping por device (Kind = enum no banco; CreateDateTime é o
        // "checado em" — CheckedAt documental é a mesma coluna).
        var deviceIds = nodes.Select(n => n.DeviceId).Distinct().ToArray();
        var latestPings = await dbContext.MonitoringResults.AsNoTracking()
            .Where(result => result.Kind == CheckKind.Ping
                && deviceIds.Contains(result.DeviceId))
            .GroupBy(result => result.DeviceId)
            .Select(group => new
            {
                DeviceId = group.Key,
                Latest = group
                    .OrderByDescending(result => result.CreateDateTime)
                    .First()
            })
            .ToDictionaryAsync(
                row => row.DeviceId,
                row => row.Latest.Succeeded,
                cancellationToken);

        var edges = await dbContext.Connections.AsNoTracking()
            .OrderBy(connection => connection.SourceInterfaceId)
            .ThenBy(connection => connection.TargetInterfaceId)
            .Select(connection => new TopologyEdgeRow(
                connection.Id,
                connection.SourceInterfaceId,
                connection.TargetInterfaceId))
            .ToListAsync(cancellationToken);

        return new TopologyGraph(
            nodes.Select(node => new TopologyNodeRow(
                node.Id,
                node.DeviceId,
                node.Name,
                node.DeviceName,
                node.DeviceType.ToString(),
                node.Ip,
                latestPings.TryGetValue(node.DeviceId, out var succeeded)
                    ? succeeded
                    : null)).ToArray(),
            edges);
    }
}
