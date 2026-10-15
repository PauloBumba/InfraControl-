using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T002c: check SnmpPrinterCounters — total de páginas da impressora
/// via <c>prtMarkerLifeCount</c> (Printer MIB RFC 3805, counter monotônico).
/// Informativo: o AlertEvaluator NÃO avalia este kind (volume mensal é diff
/// entre checks — histórico numérico fica no Zabbix, REGRA 8 ADR-028).
/// Impressoras com mono+color reporters devolvem vários indexes — reporta o
/// maior counter (monotônico por device). Valores negativos/não-numéricos
/// são descartados (counter 64-bit, guard em long).
/// </summary>
public sealed class SnmpPrinterCountersProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>prtMarkerLifeCount — total de impressões (counter).</summary>
    public const string LifeCountOid = "1.3.6.1.2.1.43.10.2.1.4";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => CheckKind.SnmpPrinterCounters.ToString();

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
                target.Trim(), LifeCountOid, DefaultTimeout, cancellationToken);
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

        var totals = variables
            .Select(variable => ParseCounter(variable.Value))
            .Where(total => total.HasValue)
            .Select(total => total!.Value)
            .ToArray();

        if (totals.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhum prtMarkerLifeCount válido encontrado na impressora.");

        return new MonitoringProviderResult(
            true, stopwatch.Elapsed, null,
            new Dictionary<string, string>
            {
                ["totalPages"] = totals.Max().ToString()
            });
    }

    /// <summary>
    /// Counter RFC 3805 é Gauge32/Counter32 (ou Counter64 em printers novas):
    /// aceita long ≥ 0; negativo ou lixo = descarta.
    /// </summary>
    private static long? ParseCounter(string? raw)
        => long.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : null;
}
