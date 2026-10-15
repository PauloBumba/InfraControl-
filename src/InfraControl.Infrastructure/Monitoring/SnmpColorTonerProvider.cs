using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T002a: check SnmpColorToner — nível do toner colorido (C/M/Y) via
/// Printer MIB prtMarkerSuppliesTable (RFC 3805). Walk na mesma tabela do
/// <see cref="SnmpTonerProvider"/>, mas classifica os carretéis por cor
/// (descrição cyan/magenta/yellow + pt-BR) e reporta o nível de cada cor
/// presente + a pior cor — insumo do AlertEvaluator (limiar 20%). Não é
/// default do Printer: opt-in por device quando o community SNMP estiver
/// liberado. Sentinela -1/-2/-3 da MIB é descartada (nível desconhecido).
/// </summary>
public sealed class SnmpColorTonerProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>prtMarkerSuppliesTable.</summary>
    public const string SuppliesTableOid = "1.3.6.1.2.1.43.11.1.1";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => CheckKind.SnmpColorToner.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target.Trim(), SuppliesTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar SNMP: {ex.Message}");
        }

        stopwatch.Stop();

        var colors = ParseColorSupplies(variables).ToArray();
        if (colors.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, "Nenhum toner colorido encontrado na impressora.");

        var worst = colors.OrderBy(supply => supply.Percent).First();
        var values = new Dictionary<string, string>();
        foreach (var supply in colors)
            values[$"{supply.Color}Percent"] = supply.Percent.ToString();
        values["worstPercent"] = worst.Percent.ToString();
        values["worstColor"] = worst.Color;

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>
    /// Classifica por descrição (cyan/magenta/yellow + pt-BR). Preto não
    /// entra aqui — é domínio do SnmpToner (monocromático, default).
    /// </summary>
    private static string? ClassifyColor(string description)
    {
        if (description.Contains("cyan", StringComparison.OrdinalIgnoreCase)
            || description.Contains("ciano", StringComparison.OrdinalIgnoreCase))
            return "cyan";
        if (description.Contains("magenta", StringComparison.OrdinalIgnoreCase))
            return "magenta";
        if (description.Contains("yellow", StringComparison.OrdinalIgnoreCase)
            || description.Contains("amarel", StringComparison.OrdinalIgnoreCase))
            return "yellow";
        return null;
    }

    private static IEnumerable<(string Color, int Percent)> ParseColorSupplies(
        IReadOnlyList<SnmpVariable> variables)
    {
        // colunas da prtMarkerSuppliesTable: 6=description, 8=maxCapacity, 9=level
        var byInstance = new Dictionary<string, (string? Description, long? Max, long? Level)>(
            StringComparer.Ordinal);

        foreach (var variable in variables)
        {
            var parts = variable.Oid.TrimEnd('.').Split('.');
            if (parts.Length < 2)
                continue;

            var column = parts[^2];
            var instance = parts[^1];
            if (!byInstance.TryGetValue(instance, out var entry))
                entry = (null, null, null);

            switch (column)
            {
                case "6":
                    entry = (variable.Value, entry.Max, entry.Level);
                    break;
                case "8" when long.TryParse(variable.Value, out var max):
                    entry = (entry.Description, max, entry.Level);
                    break;
                case "9" when long.TryParse(variable.Value, out var level):
                    entry = (entry.Description, entry.Max, level);
                    break;
                default:
                    continue;
            }

            byInstance[instance] = entry;
        }

        foreach (var (description, max, level) in byInstance.Values)
        {
            if (description is null || !level.HasValue)
                continue;

            var color = ClassifyColor(description);
            if (color is null)
                continue;

            // Sentinelas RFC 3805: -1 unknown, -2 unrestricted, -3 some remaining.
            var levelValue = level.Value;
            if (levelValue is < 0)
                continue;

            int? percent = max is > 0
                ? (int)Math.Round(levelValue * 100.0 / max.Value)
                : levelValue is >= 0 and <= 100 ? (int)levelValue : null;

            if (percent.HasValue)
                yield return (color, Math.Clamp(percent.Value, 0, 100));
        }
    }
}
