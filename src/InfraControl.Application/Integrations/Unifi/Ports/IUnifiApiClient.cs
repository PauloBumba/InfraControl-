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

    /// <summary>
    /// Configuração das redes SSID (<c>GET /rest/ssidconf</c>) — F18-T006
    /// (WiFi): nome, segurança, VLAN e bandas de cada rede.
    /// </summary>
    Task<IReadOnlyList<UnifiSsidDto>> ListSsidsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Telemetria dos APs (<c>GET /stat/device</c>) — F18-T006 (WiFi):
    /// estado/uptime, CPU/mem, tráfego, clientes e rádios (canal/largura/
    /// potência/utilização). Nada é persistido (regra 8).
    /// </summary>
    Task<IReadOnlyList<UnifiAccessPointDto>> ListAccessPointsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Sondagem leve (GET /api/self) para o health da integração.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
