using System.Globalization;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Prevention;

// F30-T004 S4 (ADR-056 aceito 2026-10-15): prevenção WiFi por TENDÊNCIA
// sobre histórico do Zabbix. Defaults em código, no espírito do
// AlertThresholds (F6) e do ADR-014 — mudar default exige release.
//
// O avaliador é PURO (sem I/O): a janela chega como séries prontas; o
// comando (EvaluateWifiPreventionCommand) é quem lê o Zabbix e aplica os
// vereditos nos Alertas. REGRA da casa: série ausente/curta = regra NÃO
// avalia (motivo honesto), nunca "saudável" inventado.

/// <summary>Limiares da prevenção WiFi (ADR-056 §3).</summary>
public static class WifiPreventionThresholds
{
    /// <summary>Utilização de rádio sustentada ≥ = Warning.</summary>
    public const int RadioUtilizationWarningPercent = 80;

    /// <summary>Utilização de rádio sustentada ≥ = Critical.</summary>
    public const int RadioUtilizationCriticalPercent = 90;

    /// <summary>A partir daqui a tendência ascendente sinaliza (ADR §3).</summary>
    public const int RadioTrendFromPercent = 70;

    /// <summary>Piso de sinal: média da janela abaixo = Warning.</summary>
    public const int RssiFloorDbm = -70;

    /// <summary>Densidade de clientes por AP sustentada > = Warning.</summary>
    public const int ClientsWarning = 50;

    /// <summary>Janela de análise (ficha §"Métricas que Precisam de Histórico").</summary>
    public const int TrendWindowMinutes = 60;

    /// <summary>Pontos mínimos da janela (ADR-056 §2 — N ≥ 12).</summary>
    public const int MinSamples = 12;

    /// <summary>Calda sustentada de Warning (~15 min @ 5 min).</summary>
    public const int SustainedSamplesWarning = 3;

    /// <summary>Calda sustentada de Critical (~10 min @ 5 min).</summary>
    public const int SustainedSamplesCritical = 2;

    /// <summary>
    /// Continuidade: vizinhas com delta maior que isso quebram a
    /// sustentação (amostra com falha de coleta não "sustenta").
    /// </summary>
    public const double MaxSampleGapMinutes = 10;
}

/// <summary>Amostra de histórico (clock do Zabbix + valor numérico).</summary>
public sealed record WifiSeriesPoint(DateTime ClockUtc, double Value);

/// <summary>Regra que DISPAROU: regra, severidade e mensagem pt-BR.</summary>
public sealed record WifiPreventionVerdict(
    string Rule, AlertSeverity Severity, string Message);

/// <summary>
/// Estado de uma regra na avaliação: <c>Evaluated</c>=false ⇒ regra não
/// roda (motivo em <c>SkipReason</c>) — ausência honesta, nunca 0.
/// </summary>
public sealed record WifiRuleEvaluation(
    string Rule, bool Evaluated, string? SkipReason, WifiPreventionVerdict? Verdict);

/// <summary>Avaliador puro de prevenção WiFi (ADR-056).</summary>
public static class WifiPreventionEvaluator
{
    public const string RadioSaturationCriticalRule = "radio-saturation-critical";
    public const string RadioSaturationRule = "radio-saturation";
    public const string RadioTrendRule = "radio-trend";
    public const string RssiDegradationRule = "rssi-degradation";
    public const string ClientDensityRule = "client-density";

    public static IReadOnlyList<WifiRuleEvaluation> Evaluate(
        string deviceName,
        IReadOnlyList<WifiSeriesPoint>? utilization,
        IReadOnlyList<WifiSeriesPoint>? rssi,
        IReadOnlyList<WifiSeriesPoint>? clients)
    {
        var results = new List<WifiRuleEvaluation>(5);

        // --- Utilização de rádio: 3 regras sobre a MESMA série ----------
        var utilSkip = SkipReason(utilization);
        if (utilSkip is not null)
        {
            results.Add(Skipped(RadioSaturationCriticalRule, utilSkip));
            results.Add(Skipped(RadioSaturationRule, utilSkip));
            results.Add(Skipped(RadioTrendRule, utilSkip));
        }
        else
        {
            var series = Ordered(utilization!);

            // Critical: calda de SustainedSamplesCritical ≥ 90.
            var criticalTail = ConsecutiveAtLeast(
                series,
                WifiPreventionThresholds.RadioUtilizationCriticalPercent,
                WifiPreventionThresholds.SustainedSamplesCritical);
            results.Add(criticalTail is null
                ? Clean(RadioSaturationCriticalRule)
                : Fired(RadioSaturationCriticalRule, AlertSeverity.Critical,
                    $"Rádio de '{deviceName}' saturado criticamente "
                    + $"({Format(series[^1].Value)}% ≥ "
                    + $"{WifiPreventionThresholds.RadioUtilizationCriticalPercent}% "
                    + $"nas últimas {criticalTail.Count} amostras consecutivas)."));

            // Warning: calda de SustainedSamplesWarning ≥ 80.
            var warningTail = ConsecutiveAtLeast(
                series,
                WifiPreventionThresholds.RadioUtilizationWarningPercent,
                WifiPreventionThresholds.SustainedSamplesWarning);
            results.Add(warningTail is null
                ? Clean(RadioSaturationRule)
                : Fired(RadioSaturationRule, AlertSeverity.Warning,
                    $"Utilização de rádio de '{deviceName}' em "
                    + $"{Format(series[^1].Value)}% "
                    + $"(≥ {WifiPreventionThresholds.RadioUtilizationWarningPercent}% "
                    + $"sustentado nas últimas {warningTail.Count} amostras)."));

            // Tendência: calda > 70 com inclinação ascendente (ADR §3:
            // "> 70% e tendência ascendente" — slope > 0, determinístico).
            var trendTail = ConsecutiveAbove(
                series,
                WifiPreventionThresholds.RadioTrendFromPercent,
                WifiPreventionThresholds.SustainedSamplesCritical);
            var slope = SlopePerMinute(series);
            results.Add(trendTail is null || slope <= 0
                ? Clean(RadioTrendRule)
                : Fired(RadioTrendRule, AlertSeverity.Warning,
                    $"Tendência ascendente de utilização em '{deviceName}' "
                    + $"({Format(series[^1].Value)}% acima de "
                    + $"{WifiPreventionThresholds.RadioTrendFromPercent}% com "
                    + $"inclinação positiva sobre "
                    + $"{WifiPreventionThresholds.TrendWindowMinutes} min)."));
        }

        // --- RSSI: piso OU queda sustentada -----------------------------
        var rssiSkip = SkipReason(rssi);
        if (rssiSkip is not null)
        {
            results.Add(Skipped(RssiDegradationRule, rssiSkip));
        }
        else
        {
            var series = Ordered(rssi!);
            var average = series.Average(point => point.Value);
            var slope = SlopePerMinute(series);

            if (average < WifiPreventionThresholds.RssiFloorDbm)
            {
                results.Add(Fired(RssiDegradationRule, AlertSeverity.Warning,
                    $"Sinal médio de '{deviceName}' abaixo do piso "
                    + $"({Format(average)} dBm < "
                    + $"{WifiPreventionThresholds.RssiFloorDbm} dBm na janela de "
                    + $"{WifiPreventionThresholds.TrendWindowMinutes} min)."));
            }
            else if (slope < 0 && IsDescendingTail(series, WifiPreventionThresholds.SustainedSamplesWarning))
            {
                results.Add(Fired(RssiDegradationRule, AlertSeverity.Warning,
                    $"Sinal de '{deviceName}' em queda sustentada "
                    + $"({Format(series[^1].Value)} dBm, inclinação "
                    + $"{Format(slope)} dBm/min)."));
            }
            else
            {
                results.Add(Clean(RssiDegradationRule));
            }
        }

        // --- Densidade de clientes --------------------------------------
        var clientsSkip = SkipReason(clients);
        if (clientsSkip is not null)
        {
            results.Add(Skipped(ClientDensityRule, clientsSkip));
        }
        else
        {
            var series = Ordered(clients!);
            var tail = ConsecutiveAbove(
                series,
                WifiPreventionThresholds.ClientsWarning,
                WifiPreventionThresholds.SustainedSamplesWarning);
            results.Add(tail is null
                ? Clean(ClientDensityRule)
                : Fired(ClientDensityRule, AlertSeverity.Warning,
                    $"Densidade de clientes em '{deviceName}' acima do limite "
                    + $"({Format(series[^1].Value)} clientes > "
                    + $"{WifiPreventionThresholds.ClientsWarning} sustentado)."));
        }

        return results;
    }

    /// <summary>null = série apta; caso contrário = motivo honesto de skip.</summary>
    private static string? SkipReason(IReadOnlyList<WifiSeriesPoint>? series)
    {
        if (series is null || series.Count == 0)
            return "métrica indisponível (sem item no Zabbix)";
        if (series.Count < WifiPreventionThresholds.MinSamples)
            return $"amostras insuficientes ({series.Count} de "
                + $"{WifiPreventionThresholds.MinSamples})";
        return null;
    }

    private static IReadOnlyList<WifiSeriesPoint> Ordered(
        IReadOnlyList<WifiSeriesPoint> series)
        => series.OrderBy(point => point.ClockUtc).ToList();

    /// <summary>Últimas N amostras contínuas todas ≥ threshold.</summary>
    private static IReadOnlyList<WifiSeriesPoint>? ConsecutiveAtLeast(
        IReadOnlyList<WifiSeriesPoint> series, double threshold, int count)
        => IsContinuousTail(series, count)
            && series.Skip(series.Count - count).All(point => point.Value >= threshold)
            ? series.Skip(series.Count - count).ToList()
            : null;

    /// <summary>Últimas N amostras contínuas todas > threshold.</summary>
    private static IReadOnlyList<WifiSeriesPoint>? ConsecutiveAbove(
        IReadOnlyList<WifiSeriesPoint> series, double threshold, int count)
        => IsContinuousTail(series, count)
            && series.Skip(series.Count - count).All(point => point.Value > threshold)
            ? series.Skip(series.Count - count).ToList()
            : null;

    /// <summary>Últimas N amostras contínuas estritamente decrescentes.</summary>
    private static bool IsDescendingTail(IReadOnlyList<WifiSeriesPoint> series, int count)
    {
        if (!IsContinuousTail(series, count))
            return false;
        var tail = series.Skip(series.Count - count).ToList();
        for (var index = 1; index < tail.Count; index++)
        {
            if (tail[index].Value >= tail[index - 1].Value)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Calda contínua: vizinhas consecutivas com delta de clock ≤
    /// <see cref="WifiPreventionThresholds.MaxSampleGapMinutes"/> — amostra
    /// perdida no meio não "sustenta" nada.
    /// </summary>
    private static bool IsContinuousTail(IReadOnlyList<WifiSeriesPoint> series, int count)
    {
        if (series.Count < count)
            return false;
        for (var index = series.Count - count + 1; index < series.Count; index++)
        {
            var gap = (series[index].ClockUtc - series[index - 1].ClockUtc).TotalMinutes;
            if (gap > WifiPreventionThresholds.MaxSampleGapMinutes)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Inclinação por mínimos quadrados em unidade/minuto (x = minutos desde
    /// a primeira amostra). Determinístico — sem ML (ADR-056 §4).
    /// </summary>
    internal static double SlopePerMinute(IReadOnlyList<WifiSeriesPoint> series)
    {
        if (series.Count < 2)
            return 0;

        var origin = series[0].ClockUtc;
        var points = series
            .Select(point => (
                X: (point.ClockUtc - origin).TotalMinutes,
                Y: point.Value))
            .ToList();

        var meanX = points.Average(point => point.X);
        var meanY = points.Average(point => point.Y);
        var numerator = points.Sum(point => (point.X - meanX) * (point.Y - meanY));
        var denominator = points.Sum(point => (point.X - meanX) * (point.X - meanX));

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static WifiRuleEvaluation Skipped(string rule, string reason)
        => new(rule, Evaluated: false, reason, null);

    private static WifiRuleEvaluation Clean(string rule)
        => new(rule, Evaluated: true, null, null);

    private static WifiRuleEvaluation Fired(
        string rule, AlertSeverity severity, string message)
        => new(rule, Evaluated: true, null,
            new WifiPreventionVerdict(rule, severity, message));

    /// <summary>Números na mensagem sempre com ponto decimal (determinístico).</summary>
    private static string Format(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
