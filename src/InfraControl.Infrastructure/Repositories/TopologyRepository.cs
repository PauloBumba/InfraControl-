using InfraControl.Application.Network.Topology.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F3-T006: grafo topológico — interface⋈device (nós) + conexões (arestas)
/// + último Ping por device (status herdado pelo node). F53-T002: camada
/// por device (BFS a partir de Router/Firewall sobre as Connections
/// reais) e metadados de ponta por aresta (SwitchPort/Vlan casados por
/// nome — sem porta cadastrada, null; nada inventado).
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
                connection.TargetInterfaceId,
                connection.Source,
                connection.Confidence))
            .ToListAsync(cancellationToken);

        var interfaceById = nodes.ToDictionary(
            node => node.Id, node => (node.DeviceId, node.Name));

        // F53-T002: hierarquia automática — BFS multi-source (raízes na
        // layer 1) sobre adjacência não-dirigida derivada das Connections
        // reais; device sem caminho até Router/Firewall fica sem camada.
        var links = new List<(Guid SourceDeviceId, Guid TargetDeviceId)>();
        foreach (var edge in edges)
        {
            if (interfaceById.TryGetValue(edge.SourceInterfaceId, out var from)
                && interfaceById.TryGetValue(edge.TargetInterfaceId, out var to))
                links.Add((from.DeviceId, to.DeviceId));
        }

        var layers = ComputeLayers(
            nodes.Select(node => (node.DeviceId, node.DeviceType)).Distinct().ToArray(),
            links);

        // Metadados de ponta (nomes, speed/duplex, VLANs) por SwitchPort
        // casada por nome — enriquece só as arestas reais.
        var enrichedEdges = await EnrichEdgesAsync(
            edges, interfaceById, cancellationToken);

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

            // Ghost = vizinho externo não gerenciado: fora da hierarquia
            // (Layer null) e sem origem/confiança persistida (F53-T002).
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
                IsGhost: true,
                SourceInterfaceName: interfaceById.TryGetValue(
                    localInterfaceId, out var ghostLocal)
                    ? ghostLocal.Name
                    : null));
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
                    : null,
                Layer: layers.TryGetValue(node.DeviceId, out var layer)
                    ? layer
                    : null))
                .Concat(ghostNodes)
                .ToArray(),
            enrichedEdges.Concat(ghostEdges).ToList());
    }

    /// <summary>
    /// F53-T002 — BFS multi-source: devices Router/Firewall entram na
    /// layer 1; vizinhos via Connections reais na seguinte (menor
    /// distância, não dirigida); não alcançado não entra no dicionário
    /// (node fica com Layer null — sem hierarquia inventada).
    /// </summary>
    private static Dictionary<Guid, int> ComputeLayers(
        IReadOnlyCollection<(Guid DeviceId, DeviceType Type)> deviceTypes,
        IReadOnlyCollection<(Guid SourceDeviceId, Guid TargetDeviceId)> links)
    {
        var adjacency = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var link in links)
        {
            if (link.SourceDeviceId == link.TargetDeviceId)
                continue;

            if (!adjacency.TryGetValue(link.SourceDeviceId, out var fromNeighbors))
                adjacency[link.SourceDeviceId] = fromNeighbors = new HashSet<Guid>();
            fromNeighbors.Add(link.TargetDeviceId);

            if (!adjacency.TryGetValue(link.TargetDeviceId, out var toNeighbors))
                adjacency[link.TargetDeviceId] = toNeighbors = new HashSet<Guid>();
            toNeighbors.Add(link.SourceDeviceId);
        }

        var layers = new Dictionary<Guid, int>();
        var queue = new Queue<(Guid DeviceId, int Layer)>();

        foreach (var root in deviceTypes
            .Where(device => device.Type is DeviceType.Router or DeviceType.Firewall)
            .Select(device => device.DeviceId))
        {
            if (layers.TryAdd(root, 1))
                queue.Enqueue((root, 1));
        }

        while (queue.Count > 0)
        {
            var (deviceId, layer) = queue.Dequeue();
            if (!adjacency.TryGetValue(deviceId, out var neighbors))
                continue;

            foreach (var neighbor in neighbors)
            {
                if (layers.TryAdd(neighbor, layer + 1))
                    queue.Enqueue((neighbor, layer + 1));
            }
        }

        return layers;
    }

    /// <summary>
    /// F53-T002 — metadados de ponta da aresta: nomes das interfaces;
    /// speed/duplex da SwitchPort casada por (device, nome) case-insensitive
    /// (mesmo critério do índice LOWER(name) do cadastro de portas) e VLANs
    /// dessa porta (SwitchPortVlan×Vlan, ordenado por número). Sem porta
    /// casada → null (nada inventado). Origem/confiança já vêm da Connection.
    /// </summary>
    private async Task<TopologyEdgeRow[]> EnrichEdgesAsync(
        IReadOnlyList<TopologyEdgeRow> edges,
        Dictionary<Guid, (Guid DeviceId, string Name)> interfaceById,
        CancellationToken cancellationToken)
    {
        var endpointIds = edges
            .SelectMany(edge => new[] { edge.SourceInterfaceId, edge.TargetInterfaceId })
            .Distinct()
            .Where(interfaceById.ContainsKey)
            .ToArray();

        Dictionary<(Guid DeviceId, string Name), SwitchPort> portByKey = [];
        if (endpointIds.Length > 0)
        {
            var endpointDeviceIds = endpointIds
                .Select(id => interfaceById[id].DeviceId)
                .Distinct()
                .ToArray();

            var ports = await dbContext.SwitchPorts.AsNoTracking()
                .Where(port => endpointDeviceIds.Contains(port.DeviceId))
                .ToListAsync(cancellationToken);

            portByKey = ports.ToDictionary(
                port => (port.DeviceId, port.Name.ToLowerInvariant()));
        }

        var matchedPortIds = endpointIds
            .Select(id => interfaceById[id])
            .Select(nic => (nic.DeviceId, nic.Name.ToLowerInvariant()))
            .Where(portByKey.ContainsKey)
            .Select(key => portByKey[key].Id)
            .Distinct()
            .ToArray();

        Dictionary<Guid, TopologyVlanRow[]> vlansByPortId = [];
        if (matchedPortIds.Length > 0)
        {
            var vlanRows = await dbContext.SwitchPortVlans.AsNoTracking()
                .Where(link => matchedPortIds.Contains(link.SwitchPortId))
                .Join(
                    dbContext.Vlans.AsNoTracking(),
                    link => link.VlanId,
                    vlan => vlan.Id,
                    (link, vlan) => new { link.SwitchPortId, vlan.VlanNumber, vlan.Name })
                .OrderBy(row => row.VlanNumber)
                .ToListAsync(cancellationToken);

            vlansByPortId = vlanRows
                .GroupBy(row => row.SwitchPortId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(row => new TopologyVlanRow(row.VlanNumber, row.Name))
                        .ToArray());
        }

        return edges.Select(edge => edge with
        {
            SourceInterfaceName = NameOf(edge.SourceInterfaceId),
            TargetInterfaceName = NameOf(edge.TargetInterfaceId),
            SourceSpeed = PortOf(edge.SourceInterfaceId)?.Speed,
            TargetSpeed = PortOf(edge.TargetInterfaceId)?.Speed,
            SourceDuplex = PortOf(edge.SourceInterfaceId)?.Duplex,
            TargetDuplex = PortOf(edge.TargetInterfaceId)?.Duplex,
            SourceVlans = VlansOf(edge.SourceInterfaceId),
            TargetVlans = VlansOf(edge.TargetInterfaceId)
        }).ToArray();

        string? NameOf(Guid interfaceId)
            => interfaceById.TryGetValue(interfaceId, out var nic) ? nic.Name : null;

        SwitchPort? PortOf(Guid interfaceId)
        {
            if (!interfaceById.TryGetValue(interfaceId, out var nic))
                return null;

            return portByKey.TryGetValue(
                (nic.DeviceId, nic.Name.ToLowerInvariant()), out var port)
                ? port
                : null;
        }

        TopologyVlanRow[]? VlansOf(Guid interfaceId)
        {
            var port = PortOf(interfaceId);
            if (port is null)
                return null;

            return vlansByPortId.TryGetValue(port.Id, out var vlans) ? vlans : [];
        }
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
