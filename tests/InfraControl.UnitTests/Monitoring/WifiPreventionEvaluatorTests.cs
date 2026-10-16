using FluentAssertions;
using InfraControl.Application.Monitoring.Prevention;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T004 S4 (ADR-056 aceito): avaliador puro de prevenção WiFi — janela
/// de histórico (Zabbix, 60 min @ 5 min) → vereditos por regra.
///
/// REGRA da casa: série ausente ou com menos de <c>MinSamples</c> pontos =
/// regra NÃO avalia (motivo honesto em pt-BR) — nunca vira "saudável"
/// inventado. "Sustentado" = N amostras CONSECUTIVAS no fim da janela com
/// continuidade (delta ≤ 10 min entre vizinhas) — anti-flapping stateless,
/// sem estado persistido (decisão registrada em docs/52 §S4).
/// Tendência = inclinação por mínimos quadrados sobre a janela.
/// </summary>
public sealed class WifiPreventionEvaluatorTests
{
    private static readonly DateTime T0 = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<WifiSeriesPoint> Series(params double[] values) =>
        values.Select((value, index) =>
            new WifiSeriesPoint(T0.AddMinutes(index * 5), value)).ToList();

    private static IReadOnlyList<WifiSeriesPoint> Flat(double value, int count = 12) =>
        Series(Enumerable.Repeat(value, count).ToArray());

    private static WifiRuleEvaluation Rule(
        IReadOnlyList<WifiRuleEvaluation> rules, string id) =>
        rules.Single(entry => entry.Rule == id);

    [Fact]
    public void HealthySeries_ShouldEvaluateAllRulesAndFireNothing()
    {
        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", Flat(30), Flat(-55), Flat(10));

        rules.Should().HaveCount(5);
        rules.Should().OnlyContain(entry => entry.Evaluated);
        rules.Should().OnlyContain(entry => entry.Verdict == null);
    }

    [Fact]
    public void SustainedSaturation_ShouldFireWarningAndCritical()
    {
        // penúltimas amostras sobem: 85 → 92 → 95 (sustenta Warning e
        // Critical no fim da janela)
        var utilization = Series(
            30, 32, 35, 38, 40, 45, 50, 55, 60, 85, 92, 95);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", utilization, Flat(-55), Flat(10));

        var critical = Rule(rules, "radio-saturation-critical");
        critical.Verdict.Should().NotBeNull();
        critical.Verdict!.Severity.Should().Be(AlertSeverity.Critical);
        critical.Verdict.Message.Should().Contain("ap-sala-01").And.Contain("95");

        var warning = Rule(rules, "radio-saturation");
        warning.Verdict.Should().NotBeNull();
        warning.Verdict!.Severity.Should().Be(AlertSeverity.Warning);
    }

    [Fact]
    public void SaturationNotSustained_ShouldNotFire()
    {
        // só a ÚLTIMA amostra passa de 80 — sem sustentação, sem alerta
        var utilization = Series(
            30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 85);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", utilization, Flat(-55), Flat(10));

        Rule(rules, "radio-saturation").Verdict.Should().BeNull();
        Rule(rules, "radio-saturation-critical").Verdict.Should().BeNull();
    }

    [Fact]
    public void RadioTrend_Above70WithAscendingSlope_ShouldWarn()
    {
        // sobe linear de 50 a 78 — passa de 70 no fim com inclinação > 0
        var utilization = Series(
            50, 52.5, 55, 57.5, 60, 62.5, 65, 67.5, 70.5, 73, 75.5, 78);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", utilization, Flat(-55), Flat(10));

        var trend = Rule(rules, "radio-trend");
        trend.Verdict.Should().NotBeNull();
        trend.Verdict!.Severity.Should().Be(AlertSeverity.Warning);
        trend.Verdict.Message.Should().Contain("ascendente");
        // abaixo de 80 sustentado ⇒ sem saturação
        Rule(rules, "radio-saturation").Verdict.Should().BeNull();
    }

    [Fact]
    public void RssiBelowFloor_ShouldWarn()
    {
        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", Flat(30), Flat(-75), Flat(10));

        var rssi = Rule(rules, "rssi-degradation");
        rssi.Verdict.Should().NotBeNull();
        rssi.Verdict!.Severity.Should().Be(AlertSeverity.Warning);
        rssi.Verdict.Message.Should().Contain("-75");
    }

    [Fact]
    public void RssiDescendingTrend_ShouldWarn_EvenAboveFloor()
    {
        // média acima do piso, mas queda sustentada de -55 a -62
        var rssi = Series(
            -55, -55.6, -56.2, -56.8, -57.4, -58, -58.6, -59.2, -59.8, -60.4, -61, -62);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", Flat(30), rssi, Flat(10));

        var verdict = Rule(rules, "rssi-degradation").Verdict;
        verdict.Should().NotBeNull();
        verdict!.Message.Should().Contain("queda");
    }

    [Fact]
    public void ClientDensityAbove50Sustained_ShouldWarn()
    {
        var clients = Series(
            10, 12, 15, 20, 25, 30, 35, 40, 45, 52, 55, 60);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", Flat(30), Flat(-55), clients);

        var density = Rule(rules, "client-density");
        density.Verdict.Should().NotBeNull();
        density.Verdict!.Severity.Should().Be(AlertSeverity.Warning);
        density.Verdict.Message.Should().Contain("60");
    }

    [Fact]
    public void MissingSeries_ShouldSkipWithHonestReason()
    {
        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", utilization: null, Flat(-55), Flat(10));

        Rule(rules, "radio-saturation").Evaluated.Should().BeFalse();
        Rule(rules, "radio-saturation").SkipReason.Should().Contain("indisponível");
        Rule(rules, "radio-saturation-critical").Evaluated.Should().BeFalse();
        Rule(rules, "radio-trend").Evaluated.Should().BeFalse();
        // as demais seguem avaliando
        Rule(rules, "rssi-degradation").Evaluated.Should().BeTrue();
        Rule(rules, "client-density").Evaluated.Should().BeTrue();
    }

    [Fact]
    public void InsufficientSamples_ShouldSkipWithCount()
    {
        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", Series(30, 30, 30, 30, 30), Flat(-55), Flat(10));

        var saturation = Rule(rules, "radio-saturation");
        saturation.Evaluated.Should().BeFalse();
        saturation.SkipReason.Should().Contain("5").And.Contain("12");
    }

    [Fact]
    public void GappedTail_ShouldNotCountAsSustained()
    {
        // 11 pontos a cada 5 min + 1 ponto 30 min depois: a calda tem
        // descontinuidade ⇒ não é "sustentado"
        var points = new List<WifiSeriesPoint>();
        for (var index = 0; index < 11; index++)
            points.Add(new WifiSeriesPoint(T0.AddMinutes(index * 5), 85));
        points.Add(new WifiSeriesPoint(T0.AddMinutes(85), 85));

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", points, Flat(-55), Flat(10));

        Rule(rules, "radio-saturation").Verdict.Should().BeNull();
        Rule(rules, "radio-saturation-critical").Verdict.Should().BeNull();
    }

    [Fact]
    public void CriticalSaturation_WithoutSustainedWarningTail_StillFires()
    {
        // subiu direto para o crítico no fim: [.., 70, 91, 94] — Warning
        // exige 3 ≥ 80 consecutivas; Critical exige só 2 ≥ 90 — as duas
        // regras têm cadência própria
        var utilization = Series(
            30, 30, 32, 35, 38, 40, 45, 50, 55, 70, 91, 94);

        var rules = WifiPreventionEvaluator.Evaluate(
            "ap-sala-01", utilization, Flat(-55), Flat(10));

        Rule(rules, "radio-saturation-critical").Verdict.Should().NotBeNull();
        // o 70 quebra a calda de Warning — cadências independentes
        Rule(rules, "radio-saturation").Verdict.Should().BeNull();
    }
}
