namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F26 Fase 4.1: cliente CONECTADO ao controller (estado atual —
/// `stat/sta`). Nunca é persistido (REGRA 8/9: telemetria de massa vive
/// só na origem; consulta sob demanda — F20-T003). `LastSeenUnix` é o
/// horário informado pelo controller (epoch), distinto do horário da
/// consulta do InfraControl. `Radio` é o valor BRUTO do campo `radio`
/// ("ng"/"na"/…) — o mapeamento para banda (2.4/5 GHz) é derivado na
/// apresentação (S1b), nunca aqui.
/// </summary>
public sealed record UnifiConnectedClientDto(
    string Mac,
    string? Ip,
    string Hostname,
    string? ApMac,
    string? SwitchMac,
    string? Essid,
    string? NetworkName,
    int? Signal,
    bool IsWired,
    long? LastSeenUnix,
    string? Radio);

/// <summary>
/// F26 Fase 4.2: rede/SSID configurado no controller (`rest/networkconf`) —
/// consulta sob demanda (mesma decisão REGRA 8/9 dos clientes; persistir
/// redes exige ADR próprio se virar cadastro).
/// </summary>
public sealed record UnifiNetworkDto(
    string Id,
    string Name,
    string Purpose,
    int? VlanId,
    bool Enabled,
    string? SiteId);

/// <summary>
/// F26 Fase 5: porta de switch tal como o controller informa em
/// `stat/device[].port_table` — identidade + estado operacional AO VIVO.
/// REGRA 8: contadores de tráfego/PoE NÃO são persistidos (consulta sob
/// demanda); "down" só é afirmado quando o controller respondeu.
/// </summary>
public sealed record UnifiSwitchPortDto(
    int PortIndex,
    string Name,
    bool? LinkUp,
    string? Speed,
    bool? FullDuplex,
    int? PortVlan,
    bool? PoeEnable,
    string? PoeStatus,
    string? Media,
    string? ClientMac);

/// <summary>
/// F26 Fase 6: nó de topologia derivado do stat/device (ao vivo —
/// REGRA 8: não persistido).
/// </summary>
public sealed record UnifiTopologyNodeDto(
    string Mac,
    string? Name,
    string Type,
    string Kind,
    bool IsKnownDevice);

/// <summary>
/// F26 Fase 6: relação device→uplink reportada pelo controller
/// (`uplink.uplink_mac`/`port_idx`/`type`). Confidence classificada:
/// "confirmed" = controller reportou; "unknown" = uplink_mac fora do
/// inventário do próprio controller (ex.: gateway do ISP, mesh).
/// </summary>
public sealed record UnifiTopologyLinkDto(
    string SourceMac,
    string? SourceName,
    string TargetMac,
    string? TargetName,
    int? SourcePort,
    string LinkType,
    string Confidence,
    string Source);

public sealed record UnifiTopologyDto(
    IReadOnlyList<UnifiTopologyNodeDto> Nodes,
    IReadOnlyList<UnifiTopologyLinkDto> Links);

/// <summary>F26 Fase 8: agregado de portas do stat/device (ao vivo).</summary>
public sealed record UnifiPortsSummaryDto(int Total, int Up);
