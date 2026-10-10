namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// Rede SSID lida do Controller (F18-T006 · WiFi): configuração vinda de
/// <c>GET /rest/ssidconf</c>. <c>Security</c> é a string bruta do UniFi
/// (wpapsk/wpa2psk/wpa3/none… — a UI mostra como vier, sem rótulo
/// inventado), <c>Vlan</c> 0 = sem VLAN e <c>Bands</c> é derivado dos
/// flags <c>no2ghz</c>/<c>no5ghz</c> (vazio = Controller não informou).
/// Nada é persistido — sondagem ao vivo com cache curto (regra 8,
/// ADR-028).
/// </summary>
public sealed record UnifiSsidDto(
    string Name,
    string Security,
    bool Enabled,
    int Vlan,
    string Bands,
    bool IsGuest);

/// <summary>
/// Rádio de AP (F18-T006): banda/canal/largura/potência de
/// <c>radio_table</c> e utilização quando o Controller expõe (0 =
/// não informado — nunca valor inventado).
/// </summary>
public sealed record UnifiRadioDto(
    string Band,
    int Channel,
    int Width,
    int TxPower,
    int Utilization);

/// <summary>
/// Telemetria de AP (F18-T006) de <c>GET /stat/device</c>: identidade +
/// estado, uptime, CPU/mem (<c>system-stats</c>), contadores de tráfego,
/// clientes somados das entradas de <c>vap_table</c> e os rádios.
/// </summary>
public sealed record UnifiAccessPointDto(
    string Mac,
    string Name,
    string Model,
    string Ip,
    bool Connected,
    long UptimeSeconds,
    int CpuPercent,
    int MemPercent,
    long RxBytes,
    long TxBytes,
    int Clients,
    IReadOnlyList<UnifiRadioDto> Radios);

/// <summary>
/// Payload do <c>GET /api/v1/integrations/unifi/wifi</c> (F18-T006):
/// redes e telemetria de AP capturados ao vivo no momento da sondagem
/// (cache Redis de 60s no handler).
/// </summary>
public sealed record UnifiWifiDto(
    IReadOnlyList<UnifiSsidDto> Ssids,
    IReadOnlyList<UnifiAccessPointDto> AccessPoints,
    DateTimeOffset CapturedAtUtc);
