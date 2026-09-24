using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Base dos providers de suprimentos de impressora (F5-T004 / Printer MIB
/// RFC 3805): walk na prtMarkerSuppliesTable, casa suprimentos por descrição
/// e devolve o pior nível em percentual.
/// </summary>
public abstract class PrinterSuppliesProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>prtMarkerSuppliesTable.</summary>
    public const string SuppliesTableOid = "1.3.6.1.2.1.43.11.1.1";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public abstract string Name { get; }

    protected abstract string SupplyLabel { get; }

    protected abstract bool Matches(string description);

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

        var matches = ParseSupplies(variables)
            .Where(supply => supply.Description is not null && Matches(supply.Description))
            .OrderBy(supply => supply.LevelPercent)
            .ToArray();

        if (matches.Length == 0)
            return new MonitoringProviderResult(
                false,
                stopwatch.Elapsed,
                $"Nenhum {SupplyLabel} encontrado na impressora.");

        var worst = matches[0];
        return new MonitoringProviderResult(
            true,
            stopwatch.Elapsed,
            null,
            new Dictionary<string, string>
            {
                ["levelPercent"] = worst.LevelPercent.ToString(),
                ["description"] = worst.Description!
            });
    }

    private static IEnumerable<Supply> ParseSupplies(IReadOnlyList<SnmpVariable> variables)
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
            if (!level.HasValue)
                continue;

            var levelValue = level.Value;
            int? percent = max is > 0
                ? (int)Math.Round(levelValue * 100.0 / max.Value)
                : levelValue is >= 0 and <= 100 ? (int)levelValue : null;

            if (percent.HasValue)
                yield return new Supply(description, Math.Clamp(percent.Value, 0, 100));
        }
    }

    private sealed record Supply(string? Description, int LevelPercent);
}

/// <summary>Toner (prtMarkerSuppliesType=toner ou descrição com toner/ink/tinta).</summary>
public sealed class SnmpTonerProvider(ISnmpClient snmpClient) : PrinterSuppliesProvider(snmpClient)
{
    public override string Name => CheckKind.SnmpToner.ToString();

    protected override string SupplyLabel => "toner";

    protected override bool Matches(string description)
        => description.Contains("toner", StringComparison.OrdinalIgnoreCase)
           || description.Contains("ink", StringComparison.OrdinalIgnoreCase)
           || description.Contains("tinta", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Drum/cilindro (descrição com drum/cilindro/imaging).</summary>
public sealed class SnmpDrumProvider(ISnmpClient snmpClient) : PrinterSuppliesProvider(snmpClient)
{
    public override string Name => CheckKind.SnmpDrum.ToString();

    protected override string SupplyLabel => "drum";

    protected override bool Matches(string description)
        => description.Contains("drum", StringComparison.OrdinalIgnoreCase)
           || description.Contains("cilindro", StringComparison.OrdinalIgnoreCase)
           || description.Contains("imaging", StringComparison.OrdinalIgnoreCase);
}
