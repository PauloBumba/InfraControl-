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

    // Walk v1 é sequencial (sem GetBulk) — margem maior que o v2c.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    public abstract string Name { get; }

    protected abstract string SupplyLabel { get; }

    protected abstract bool Matches(string description);

    /// <summary>
    /// Cilindro integrado ao toner (cartucho all-in-one, ex.: Canon
    /// imageRUNNER T06/GPR): quando a impressora não expõe nenhuma entrada
    /// de drum na tabela, o cilindro É o cartucho de toner — o nível é o
    /// mesmo. Só o SnmpDrum liga este fallback.
    /// </summary>
    protected virtual bool TonerFallback => false;

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
            // Impressoras HSDS só respondem SNMPv1 (mesma versão que o
            // Zabbix usa nos hosts prn-*/HSDS_*); v2c estoura timeout.
            variables = await snmpClient.WalkAsync(
                target.Trim(), SuppliesTableOid, DefaultTimeout,
                SnmpVersion.V1, cancellationToken);
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

        var supplies = ParseSupplies(variables).ToArray();
        var matches = supplies
            .Where(supply => supply.Description is not null && Matches(supply.Description))
            .OrderBy(supply => supply.LevelPercent)
            .ToArray();

        // Fallback all-in-one: sem nenhuma descrição de drum na tabela, o
        // cilindro é o cartucho de toner — reporta o pior toner não-colorido.
        if (matches.Length == 0 && TonerFallback
            && !DescribeAll(variables).Any(description => Matches(description)))
        {
            matches = supplies
                .Where(supply => supply.Description is not null
                    && IsTonerCartridgeDescription(supply.Description))
                .OrderBy(supply => supply.LevelPercent)
                .ToArray();
        }

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

    /// <summary>Todas as descrições (coluna 6) da tabela, com ou sem nível.</summary>
    private static IEnumerable<string> DescribeAll(IReadOnlyList<SnmpVariable> variables)
    {
        const string prefix = SuppliesTableOid + ".";
        foreach (var variable in variables)
        {
            if (!variable.Oid.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rest = variable.Oid[prefix.Length..].TrimEnd('.');
            var dot = rest.IndexOf('.');
            if (dot <= 0 || rest[..dot] != "6")
                continue;

            yield return variable.Value;
        }
    }

    /// <summary>
    /// Cartucho de toner (não garrafa de tinta): o cilindro integrado só
    /// existe em cartucho toner — impressora de tanque (Epson EcoTank) não
    /// tem cilindro e não deve herdar o nível da tinta no SnmpDrum.
    /// </summary>
    internal static bool IsTonerCartridgeDescription(string description)
        => description.Contains("toner", StringComparison.OrdinalIgnoreCase)
           && !description.Contains("cyan", StringComparison.OrdinalIgnoreCase)
           && !description.Contains("ciano", StringComparison.OrdinalIgnoreCase)
           && !description.Contains("magenta", StringComparison.OrdinalIgnoreCase)
           && !description.Contains("yellow", StringComparison.OrdinalIgnoreCase)
           && !description.Contains("amarel", StringComparison.OrdinalIgnoreCase);

    /// <summary>Toner/ink/tinta sem tons de cor (cores são do SnmpColorToner).</summary>
    internal static bool IsTonerDescription(string description)
    {
        var isToner = description.Contains("toner", StringComparison.OrdinalIgnoreCase)
           || description.Contains("ink", StringComparison.OrdinalIgnoreCase)
           || description.Contains("tinta", StringComparison.OrdinalIgnoreCase);
        if (!isToner)
            return false;

        return !(description.Contains("cyan", StringComparison.OrdinalIgnoreCase)
            || description.Contains("ciano", StringComparison.OrdinalIgnoreCase)
            || description.Contains("magenta", StringComparison.OrdinalIgnoreCase)
            || description.Contains("yellow", StringComparison.OrdinalIgnoreCase)
            || description.Contains("amarel", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Supply> ParseSupplies(IReadOnlyList<SnmpVariable> variables)
    {
        // OIDs reais das impressoras (confirmado no Zabbix HSDS): a instância
        // é composta (ex.: "1.1"), então depois do prefixo da tabela sobra
        // "<coluna>.<instancia...>" — coluna 6=description, 8=maxCapacity,
        // 9=level (numeração que os agentes Canon/HP/Epson expõem).
        var byInstance = new Dictionary<string, (string? Description, long? Max, long? Level)>(
            StringComparer.Ordinal);

        const string prefix = SuppliesTableOid + ".";
        foreach (var variable in variables)
        {
            if (!variable.Oid.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rest = variable.Oid[prefix.Length..].TrimEnd('.');
            var dot = rest.IndexOf('.');
            if (dot <= 0)
                continue;

            var column = rest[..dot];
            var instance = rest[(dot + 1)..];
            if (instance.Length == 0)
                continue;

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

            // Sentinelas RFC 3805: -1 unknown, -2 unlimited, -3 some remaining.
            var levelValue = level.Value;
            if (levelValue < 0)
                continue;

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
        => IsTonerDescription(description);
}

/// <summary>Drum/cilindro (descrição com drum/cilindro/imaging).</summary>
public sealed class SnmpDrumProvider(ISnmpClient snmpClient) : PrinterSuppliesProvider(snmpClient)
{
    public override string Name => CheckKind.SnmpDrum.ToString();

    protected override string SupplyLabel => "drum";

    // Canon imageRUNNER (T06) e similares: cilindro integrado ao toner —
    // sem entrada de drum na tabela, o nível do cilindro é o do toner.
    protected override bool TonerFallback => true;

    protected override bool Matches(string description)
        => description.Contains("drum", StringComparison.OrdinalIgnoreCase)
           || description.Contains("cilindro", StringComparison.OrdinalIgnoreCase)
           || description.Contains("imaging", StringComparison.OrdinalIgnoreCase);
}
