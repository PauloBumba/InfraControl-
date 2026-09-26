namespace InfraControl.Application.Network.Topology.Ports;

// F3-T006: leitura do grafo topológico (só backend — nodes = interfaces
// incluindo isoladas, edges = Connections do F3-T005, status herdado do
// último Ping do device dono).

public interface ITopologyRepository
{
    Task<TopologyGraph> GetGraphAsync(CancellationToken cancellationToken = default);
}

/// <summary>Node = interface com o device resolvido.</summary>
public sealed record TopologyNodeRow(
    Guid InterfaceId,
    Guid DeviceId,
    string Name,
    string DeviceName,
    string DeviceType,
    string? Ip,
    bool? LatestPingSucceeded);

/// <summary>Edge = Connection dirigida entre duas interfaces.</summary>
public sealed record TopologyEdgeRow(
    Guid ConnectionId,
    Guid SourceInterfaceId,
    Guid TargetInterfaceId);

public sealed record TopologyGraph(
    IReadOnlyList<TopologyNodeRow> Nodes,
    IReadOnlyList<TopologyEdgeRow> Edges);
