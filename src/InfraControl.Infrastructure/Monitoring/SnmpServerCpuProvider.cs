using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T003a: check SnmpServerCpu — CPU por core via <c>hrProcessorLoad</c>
/// (HOST-RESOURCES-MIB RFC 2790, universal Windows/Linux). Reporta o pior
/// core (alertável), a média, a lista de cores e — best-effort — o total de
/// processos (<c>hrSystemProcesses</c>, informativo sem regra: ficha 44 §E).
/// hrProcessorLoad é Gauge 0..100: valores fora da faixa são descartados.
/// Opt-in — não é default do Server.
/// </summary>
public sealed class SnmpServerCpuProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>hrProcessorTable.2 — CPU load % por processador.</summary>
    public const string ProcessorLoadOid = "1.3.6.1.2.1.25.3.3.1.2";

    /// <summary>hrSystemProcesses — contagem de processos (best-effort).</summary>
    public const string ProcessesOid = "1.3.6.1.2.1.25.1.6.0";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpServerCpu.ToString();

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
        IReadOnlyList<SnmpVariable> loads;
        try
        {
            loads = await snmpClient.WalkAsync(
                target.Trim(), ProcessorLoadOid, DefaultTimeout, cancellationToken);
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

        var percents = loads
            .Select(variable => ParseLoad(variable.Value))
            .Where(percent => percent.HasValue)
            .Select(percent => percent!.Value)
            .ToArray();

        if (percents.Length == 0)
        {
            stopwatch.Stop();
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhum hrProcessorLoad válido encontrado no servidor.");
        }

        var values = new Dictionary<string, string>
        {
            ["cpuPercent"] = percents.Max().ToString(),
            ["averagePercent"] = ((int)Math.Round(
                percents.Average(), MidpointRounding.AwayFromZero)).ToString(),
            ["coreCount"] = percents.Length.ToString(),
            ["corePercents"] = string.Join(',', percents)
        };

        // hrSystemProcesses é best-effort: agent sem a coluna não derruba a CPU.
        try
        {
            var processes = await snmpClient.WalkAsync(
                target.Trim(), ProcessesOid, DefaultTimeout, cancellationToken);
            var processCount = processes
                .Select(variable => ParseCount(variable.Value))
                .FirstOrDefault(count => count.HasValue);
            if (processCount.HasValue)
                values["processCount"] = processCount!.Value.ToString();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // informativo — indisponível não falha o check.
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>hrProcessorLoad é 0..100 (RFC 2790): fora da faixa = descarta.</summary>
    private static int? ParseLoad(string? raw)
        => int.TryParse(raw, out var parsed) && parsed is >= 0 and <= 100
            ? parsed
            : null;

    /// <summary>hrSystemProcesses é Integer32 ≥ 0: negativo/não-numérico = descarta.</summary>
    private static long? ParseCount(string? raw)
        => long.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : null;
}
