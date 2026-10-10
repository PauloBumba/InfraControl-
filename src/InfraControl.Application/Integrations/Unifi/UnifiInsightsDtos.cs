namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// Tráfego por cliente (F18-T007 · docs/29 §Insights): vêm de
/// <c>GET /stat/sta</c> do Controller — contadores acumulados desde o
/// último reset do cliente (não são persistidos: sondagem ao vivo,
/// mesma regra 8 da integração).
/// </summary>
public sealed record UnifiClientTrafficDto(
    string Mac,
    string Hostname,
    string Ip,
    long RxBytes,
    long TxBytes,
    DateTime? LastSeen)
{
    public long TotalBytes => RxBytes + TxBytes;
}

/// <summary>
/// DPI por aplicação (F18-T007 · docs/29 §Insights): vêm de
/// <c>GET /stat/app</c> — <c>value</c> = bytes totais vistos pela
/// inspeção de pacotes.
/// </summary>
public sealed record UnifiDpiAppDto(
    string App,
    string Category,
    long TotalBytes);

/// <summary>
/// Painéis do /insights (F18-T007): tráfego por cliente e DPI por
/// aplicação, capturados ao vivo no momento da chamada. Speed test
/// NÃO existe no Controller — fica de fora por decisão de escopo.
/// </summary>
public sealed record UnifiInsightsDto(
    IReadOnlyList<UnifiClientTrafficDto> TrafficPerClient,
    IReadOnlyList<UnifiDpiAppDto> DpiPerApplication,
    DateTimeOffset CapturedAtUtc);
