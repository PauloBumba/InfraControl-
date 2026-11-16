using System.Globalization;
using System.Text.RegularExpressions;

namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>
/// ADR-069: extrai o threshold numérico da expressão de uma trigger do
/// Zabbix (ex.: <c>{10944:icmppingloss.last()} &gt; 5</c> → 5). Heurística
/// deliberada: primeiro comparador seguido de número vence; expressões
/// sem número (nodata, funções puras) devolvem null e o cartão sai sem
/// linha de limite. O Zabbix não tem "limite de atenção vs crítico"
/// separados — a expressão da trigger é a única fonte real.
/// </summary>
public static partial class ZabbixTriggerThreshold
{
    [GeneratedRegex(@"(>=|<=|!=|<>|=|>|<)\s*(-?\d+(?:[.,]\d+)?)")]
    private static partial Regex ComparatorPattern();

    /// <summary>Threshold da expressão de problema; null quando não há número comparado.</summary>
    public static double? TryParseProblem(string? expression)
        => TryParse(expression);

    /// <summary>Threshold da expressão de recovery (contexto do RESOLVIDO).</summary>
    public static double? TryParseRecovery(string? expression)
        => TryParse(expression);

    private static double? TryParse(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var match = ComparatorPattern().Match(expression);
        if (!match.Success)
            return null;

        var raw = match.Groups[2].Value.Replace(',', '.');
        return double.TryParse(raw, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? value
            : null;
    }
}
