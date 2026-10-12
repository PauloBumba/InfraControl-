using InfraControl.Application.Network.Topology.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.Topology;

// F3-T006: GetTopology — grafo para o mapa (nodes = interfaces, edges =
// Connections). Status = "up"/"down" do último Ping do device dono do
// node; null = nunca verificado. Falhas embrulhadas pelo GlobalExceptionHandler.

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
    string? Port = null);

/// <summary>Edge do mapa = uma Connection dirigida (ou ghost, F29-5).</summary>
public sealed record TopologyEdgeDto(
    Guid Id,
    Guid SourceInterfaceId,
    Guid TargetInterfaceId,
    bool IsGhost = false);

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
                node.Port)).ToArray(),
            graph.Edges.Select(edge => new TopologyEdgeDto(
                edge.ConnectionId,
                edge.SourceInterfaceId,
                edge.TargetInterfaceId,
                edge.IsGhost)).ToArray());
    }
}
