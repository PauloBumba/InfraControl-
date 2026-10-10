namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// Porta de saída para o UniFi Controller (F20-T001/ADR-028 §2/§5) —
/// autenticação por sessão <c>unifises</c> e re-auth automática em 401
/// ficam na implementação; aqui só o contrato de cadastro/health.
/// </summary>
public interface IUnifiApiClient
{
    /// <summary>Lista os dispositivos (Gateway/Switch/AP) do Controller.</summary>
    Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tráfego por cliente ativo (<c>GET /stat/sta</c>) — F18-T007
    /// (Insights): contadores acumulados do Cliente (rx/tx bytes).
    /// </summary>
    Task<IReadOnlyList<UnifiClientTrafficDto>> ListClientTrafficAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DPI por aplicação (<c>GET /stat/app</c>) — F18-T007 (Insights):
    /// bytes vistos pela inspeção de pacotes, por app/categoria.
    /// </summary>
    Task<IReadOnlyList<UnifiDpiAppDto>> ListDpiStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Sondagem leve (GET /api/self) para o health da integração.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 3.1: sites do controller (GET /api/self/sites).</summary>
    Task<IReadOnlyList<UnifiSiteDto>> ListSitesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 3: versão do firmware do controller (stat/sysinfo).</summary>
    Task<string> GetSystemVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 4.1: clientes conectados AGORA (stat/sta — não persiste).</summary>
    Task<IReadOnlyList<UnifiConnectedClientDto>> ListConnectedClientsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 4.2: redes/SSIDs configurados (rest/networkconf).</summary>
    Task<IReadOnlyList<UnifiNetworkDto>> ListNetworksAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F26 Fase 5: portas do switch com o MAC informado (port_table do
    /// stat/device — endpoint já validado; filtro do lado do cliente).
    /// </summary>
    Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
        string deviceMac, CancellationToken cancellationToken = default);

    /// <summary>
    /// F26 Fase 6: topologia derivada do stat/device (uplink + LLDP
    /// quando presente) — consulta ao vivo, nunca persistida.
    /// </summary>
    Task<UnifiTopologyDto> GetTopologyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>F26 Fase 8: total de portas e portas com link up (vivo).</summary>
    Task<UnifiPortsSummaryDto> GetPortsSummaryAsync(
        CancellationToken cancellationToken = default);
}
