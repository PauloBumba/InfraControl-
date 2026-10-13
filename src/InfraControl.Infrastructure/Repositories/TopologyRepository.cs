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

        // F29-5: nós "ghost" = vizinhos LLDP/CDP descobertos mas NÃO
        // gerenciados (achado Neighbor sem DeviceId — hoje descartados da
        // ingestão de Connections). Vira nó tracejado + aresta porta-a-porta
        // até a interface local, sem nova persistência (REGRA 8 — só
        // read-model a partir de achados já gravados). Mesma semântica de
        // staleness das Connections: um vizinho que sumiu fica até o achado
        // ser podado.
        var interfaceLookup = nodes
            .GroupBy(node => (node.DeviceId, node.Name))
            .ToDictionary(group => group.Key, group => group.First().Id);

        var ghostFindings = await (
            from result in dbContext.DiscoveryResults.AsNoTracking()
            join run in dbContext.DiscoveryRuns.AsNoTracking()
                on result.DiscoveryRunId equals run.Id
            where result.Kind == DiscoveryResultKind.Neighbor
                && result.DeviceId == null
                && result.Values != null
            orderby result.CreatedAt descending
            select new
            {
                result.Id,
                result.Values,
                LocalDeviceId = run.DeviceId
            }).ToListAsync(cancellationToken);

        var ghostNodes = new List<TopologyNodeRow>();
        var ghostEdges = new List<TopologyEdgeRow>();
        var seenGhosts = new HashSet<(Guid LocalInterface, string Name, string Port)>();

        foreach (var finding in ghostFindings)
        {
            var values = finding.Values;
            if (values is null)
                continue;

            if (!values.TryGetValue("local_if_name", out var localIf)
                || string.IsNullOrWhiteSpace(localIf)
                || !interfaceLookup.TryGetValue(
                    (finding.LocalDeviceId, localIf.Trim()), out var localInterfaceId))
                continue;

            // Mesmo fallback de nome/porta do LldpDiscoveryProvider.
            var name = FirstValue(values, ["sys_name", "chassis_id", "port_id"])
                ?? "Vizinho desconhecido";
            var port = FirstValue(values, ["port_desc", "port_id"]);

            if (!seenGhosts.Add((localInterfaceId, name, port ?? string.Empty)))
                continue;

            ghostNodes.Add(new TopologyNodeRow(
                finding.Id, // id sintético estável = próprio achado
                finding.LocalDeviceId,
                name,
                name, // device ghost não gerenciado — label é o próprio nome
                "Unknown",
                null,
                null,
                IsGhost: true,
                Port: port));

            ghostEdges.Add(new TopologyEdgeRow(
                finding.Id,
                localInterfaceId,
                finding.Id,
                IsGhost: true));
        }

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
                    : null))
                .Concat(ghostNodes)
                .ToArray(),
            edges.Concat(ghostEdges).ToList());
    }

    private static string? FirstValue(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
