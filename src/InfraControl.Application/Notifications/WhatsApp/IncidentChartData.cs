namespace InfraControl.Application.Notifications.WhatsApp;

/// <summary>
/// F33-T005/ADR-059 decisão 7: cartão visual enviado ao grupo —
/// Incidente (Critical/abertura), Aviso (Warning) e Resolução (OK).
/// </summary>
public enum ChartCardKind
{
    Incident,
    Warning,
    Resolution
}

/// <summary>Ponto da série histórica (horário UTC + valor numérico).</summary>
public sealed record ChartSeriesPoint(DateTime AtUtc, double Value);

/// <summary>
/// Dados de entrada do <see cref="IIncidentImageRenderer"/> — o renderer
/// (SkiaSharp, ADR-059) transforma em PNG; sem série o cartão sai com o
/// aviso "dados indisponíveis" (nunca bloqueia o envio — fallback texto
/// obrigatório no dispatcher).
/// </summary>
public sealed record IncidentChartData(
    ChartCardKind Kind,
    string Title,
    string? DeviceLabel,
    string SeverityLabel,
    DateTime EventAtUtc,
    IReadOnlyList<ChartSeriesPoint> Series,
    double? LimitValue = null)
{
    public double? CurrentValue => Series.Count > 0 ? Series[^1].Value : null;
    public double? MinValue => Series.Count > 0 ? Series.Min(point => point.Value) : null;
    public double? MaxValue => Series.Count > 0 ? Series.Max(point => point.Value) : null;
}

/// <summary>Porta de renderização do cartão (implementação SkiaSharp em Infrastructure).</summary>
public interface IIncidentImageRenderer
{
    byte[] RenderPng(IncidentChartData data);
}

/// <summary>
/// Monta o <see cref="IncidentChartData"/> de uma delivery a partir da
/// série histórica do Zabbix (spike F33-T005: fonte = API history).
/// Devolve <c>null</c> quando não há dado (Zabbix sem config, device sem
/// host correspondente, série vazia) — o canal degrada para texto.
/// </summary>
public interface IWhatsAppChartFactory
{
    Task<IncidentChartData?> TryCreateAsync(
        Guid? deviceId,
        string title,
        string eventType,
        DateTime eventAtUtc,
        CancellationToken cancellationToken = default);
}
