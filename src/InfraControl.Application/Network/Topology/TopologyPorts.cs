using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.Topology.Ports;

// F3-T006: leitura do grafo topológico (só backend — nodes = interfaces
// incluindo isoladas, edges = Connections do F3-T005, status herdado do
// último Ping do device dono). F53-T002: nodes ganham `layer` (BFS a
// partir de Router/Firewall sobre as Connections reais) e edges ganham
// metadados de ponta (nomes das portas, speed/duplex da SwitchPort casada
// por nome, VLANs dessa porta, origem/confiança da Connection).

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
    bool? LatestPingSucceeded,
    bool IsGhost = false,
    string? Port = null,
    int? Layer = null);

/// <summary>
/// Edge = Connection dirigida entre duas interfaces, com metadados das
/// pontas (F53-T002). Speed/Duplex/Vlans null = sem SwitchPort casado
/// (nada inventado); Source/Confidence null = não é Connection persistida
/// (ghost — F29-5).
/// </summary>
public sealed record TopologyEdgeRow(
    Guid ConnectionId,
    Guid SourceInterfaceId,
    Guid TargetInterfaceId,
    ConnectionSource? Source = null,
    ConnectionConfidence? Confidence = null,
    bool IsGhost = false,
    string? SourceInterfaceName = null,
    string? TargetInterfaceName = null,
    SwitchPortSpeed? SourceSpeed = null,
    SwitchPortSpeed? TargetSpeed = null,
    SwitchPortDuplex? SourceDuplex = null,
    SwitchPortDuplex? TargetDuplex = null,
    TopologyVlanRow[]? SourceVlans = null,
    TopologyVlanRow[]? TargetVlans = null);

/// <summary>VLAN atribuída a uma ponta da aresta (F53-T002).</summary>
public sealed record TopologyVlanRow(int Number, string Name);

public sealed record TopologyGraph(
    IReadOnlyList<TopologyNodeRow> Nodes,
    IReadOnlyList<TopologyEdgeRow> Edges);
