namespace InfraControl.Application.Network.Explorer;

/// <summary>
/// Uma evidência de rede para o MAC/IP pesquisado (Network Explorer F1):
/// uma linha por achado de discovery (MacTableEntry, ArpEntry, DhcpLease,
/// Neighbor) enriquecida com o device que originou a coleta.
/// </summary>
public sealed record MacLookupSightingDto(
    string Kind,
    string KindLabel,
    Guid? DeviceId,
    string? DeviceName,
    string? DeviceIp,
    string? Port,
    string? PortDescription,
    string? Mac,
    string? Ip,
    string? Hostname,
    string? Vlan,
    DateTime SeenAt,
    int AgeDays);

/// <summary>
/// Visão agregada do MAC Explorer: resumo do que se sabe sobre o termo
/// (MAC canônico, IP, hostname, VLAN, AP Wi-Fi quando o UniFi souber) +
/// todas as evidências encontradas. Campos null = fonte não informou —
/// nunca inventado.
/// </summary>
public sealed record MacLookupResultDto(
    string Term,
    string? MatchedMac,
    string? Ip,
    string? Hostname,
    string? Vlan,
    bool WifiClient,
    string? WifiApName,
    string? WifiSsid,
    int? WifiSignal,
    bool IsStale,
    DateTime? LastSeenAt,
    IReadOnlyList<MacLookupSightingDto> Sightings);
