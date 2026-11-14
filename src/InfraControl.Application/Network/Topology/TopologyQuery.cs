using InfraControl.Application.Network.Connections;
using InfraControl.Application.Network.Topology.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.Topology;

// F3-T006: GetTopology — grafo para o mapa (nodes = interfaces, edges =
// Connections). Status = "up"/"down" do último Ping do device dono do
// node; null = nunca verificado. Falhas embrulhadas pelo GlobalExceptionHandler.
// F53-T002: node ganha `layer` (hierarquia automática por BFS a partir de
// Router/Firewall) e edge ganha metadados de ponta (portas, speed/duplex,
// VLANs, origem/confiança).

/// <summary>Grafo topológico da rede.</summary>
public sealed record TopologyDto(
    TopologyNodeDto[] Nodes,
    TopologyEdgeDto[] Edges);

/// <summary>Node do mapa = uma interface (ou um vizinho ghost, F29-5).</summary>
public sealed record TopologyNodeDto(
    Guid Id,
    Guid DeviceId,
    string Name,
    string DeviceName,
    string DeviceType,
    string? Ip,
    string? Status,
    bool IsGhost = false,
    string? Port = null,
    int? Layer = null);

/// <summary>VLAN numa ponta da aresta — número + nome (F53-T002).</summary>
public sealed record TopologyVlanDto(int Number, string Name);

/// <summary>
/// Edge do mapa = uma Connection dirigida (ou ghost, F29-5). Metadados de
/// ponta casam SwitchPort por nome da interface (case-insensitive); sem
/// porta cadastrada os campos ficam null — nunca inventar valor. Origem e
/// confiança vêm da Connection em minúsculas (F53-T001, igual ao UniFi
/// live); ghost = null (não é Connection persistida).
/// </summary>
public sealed record TopologyEdgeDto(
    Guid Id,
    Guid SourceInterfaceId,
    Guid TargetInterfaceId,
    bool IsGhost = false,
    string? SourceInterfaceName = null,
    string? TargetInterfaceName = null,
    SwitchPortSpeed? SourceSpeed = null,
    SwitchPortSpeed? TargetSpeed = null,
    SwitchPortDuplex? SourceDuplex = null,
    SwitchPortDuplex? TargetDuplex = null,
    TopologyVlanDto[]? SourceVlans = null,
    TopologyVlanDto[]? TargetVlans = null,
    string? Source = null,
    string? Confidence = null);

public sealed record GetTopologyQuery : IQuery<TopologyDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTopologyQueryHandler(ITopologyRepository repository)
    : IRequestHandler<GetTopologyQuery, TopologyDto>
{
    public async Task<TopologyDto> Handle(
        GetTopologyQuery request,
        CancellationToken cancellationToken)
    {
        var graph = await repository.GetGraphAsync(cancellationToken);

        return new TopologyDto(
            graph.Nodes.Select(node => new TopologyNodeDto(
                node.InterfaceId,
                node.DeviceId,
                node.Name,
                node.DeviceName,
                node.DeviceType,
                node.Ip,
                node.LatestPingSucceeded switch
                {
                    true => "up",
                    false => "down",
                    null => null
                },
                node.IsGhost,
                node.Port,
                node.Layer)).ToArray(),
            graph.Edges.Select(edge => new TopologyEdgeDto(
                edge.ConnectionId,
                edge.SourceInterfaceId,
                edge.TargetInterfaceId,
                edge.IsGhost,
                edge.SourceInterfaceName,
                edge.TargetInterfaceName,
                edge.SourceSpeed,
                edge.TargetSpeed,
                edge.SourceDuplex,
                edge.TargetDuplex,
                edge.SourceVlans?.Select(
                    vlan => new TopologyVlanDto(vlan.Number, vlan.Name)).ToArray(),
                edge.TargetVlans?.Select(
                    vlan => new TopologyVlanDto(vlan.Number, vlan.Name)).ToArray(),
                edge.Source?.ToWire(),
                edge.Confidence?.ToWire())).ToArray());
    }
}
