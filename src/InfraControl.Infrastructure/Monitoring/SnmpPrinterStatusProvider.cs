using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T002b: check SnmpPrinterStatus — estado agregado da impressora via
/// Printer MIB (RFC 3805): bandejas (<c>prtInputStatus</c>), tampa
/// (<c>prtCoverStatus</c>) e alertas ativos (<c>prtAlertTable</c>).
/// Padrão F29-3: agregado por device; prtInputStatus é a coluna essencial,
/// cover/alerts são best-effort (MIB parcial ainda vale). Linha idle da
/// prtAlertTable (code other/unknown) não conta como alerta ativo.
/// Opt-in — não é default do Printer.
/// </summary>
public sealed class SnmpPrinterStatusProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>prtInputStatus — status da bandeja de entrada.</summary>
    public const string InputStatusOid = "1.3.6.1.2.1.43.8.2.1.9";

    /// <summary>prtCoverStatus — status da tampa/interlock.</summary>
    public const string CoverStatusOid = "1.3.6.1.2.1.43.6.1.1.3";

    /// <summary>prtAlertTable — tabela de alertas (code=col 1, severity=col 2).</summary>
    public const string AlertTableOid = "1.3.6.1.2.1.43.18";

    // prtInputStatus (RFC 3805)
    private const string InputStatusJam = "5";       // jammed
    private const string InputStatusMediaEmpty = "9"; // mediaEmpty
    private const string InputStatusMediaJam = "10";  // mediaJammed
    private const string InputStatusLevelEmpty = "16"; // inputLevelEmpty

    // prtCoverStatus (RFC 3805): aberta = doorOpen/interlockOpen/open.
    private static readonly HashSet<string> CoverOpenValues = ["3", "5", "7"];

    // prtAlertCode: other(1)/unknown(2) = linha idle, não é alerta ativo.
    private const long IdleAlertCodeMax = 2;

    // prtAlertSeverityLevel: critical(3) major(4) minor(5) warning(6)
    // informational(7) — menor número real = pior.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpPrinterStatus.ToString();

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

        // prtInputStatus é essencial (Printer MIB base) — sem ela o device
        // não fala Printer MIB de entrada; as demais são best-effort.
        IReadOnlyList<SnmpVariable> inputs;
        try
        {
            inputs = await snmpClient.WalkAsync(
                target.Trim(), InputStatusOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                $"Falha ao consultar prtInputStatus: {exception.Message}");
        }

        IReadOnlyList<SnmpVariable> covers = [];
        try
        {
            covers = await snmpClient.WalkAsync(
                target.Trim(), CoverStatusOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // MIB parcial — cover indisponível não derruba o check.
        }

        IReadOnlyList<SnmpVariable> alerts = [];
        try
        {
            alerts = await snmpClient.WalkAsync(
                target.Trim(), AlertTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // MIB parcial — alertas indisponíveis não derrubam o check.
        }

        stopwatch.Stop();

        if (inputs.Count == 0 && covers.Count == 0 && alerts.Count == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Printer-MIB não respondeu "
                    + "(prtInputStatus/prtCoverStatus/prtAlertTable vazios).");

        var inputCount = inputs.Count(variable => IsStatusValue(variable.Value));
        var inputEmptyCount = inputs.Count(variable =>
            variable.Value is InputStatusMediaEmpty or InputStatusLevelEmpty);
        var inputJammedCount = inputs.Count(variable =>
            variable.Value is InputStatusJam or InputStatusMediaJam);
        var coverOpen = covers.Any(variable => CoverOpenValues.Contains(variable.Value));

        var active = ParseActiveAlerts(alerts).ToArray();
        var values = new Dictionary<string, string>
        {
            ["inputCount"] = inputCount.ToString(),
            ["inputEmptyCount"] = inputEmptyCount.ToString(),
            ["inputJammedCount"] = inputJammedCount.ToString(),
            ["coverOpen"] = coverOpen ? "true" : "false",
            ["activeAlertCount"] = active.Length.ToString()
        };

        if (active.Length > 0)
            values["worstAlertSeverity"] = MapSeverity(active.Min());

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>Conta valores de status plausíveis (não-numeric garbage skip).</summary>
    private static bool IsStatusValue(string? value)
        => long.TryParse(value, out var parsed) && parsed is >= 1 and <= 16;

    /// <summary>
    /// Linhas da prtAlertTable com code real (&gt; other/unknown) = alertas
    /// ativos; devolve a severidade de cada um.
    /// </summary>
    private static IEnumerable<long> ParseActiveAlerts(
        IReadOnlyList<SnmpVariable> variables)
    {
        // colunas: ...43.18.1.1.{idx} = code, ...43.18.1.2.{idx} = severity
        var byInstance = new Dictionary<string, (long? Code, long? Severity)>(
            StringComparer.Ordinal);

        foreach (var variable in variables)
        {
            var parts = variable.Oid.TrimEnd('.').Split('.');
            if (parts.Length < 3)
                continue;

            var column = parts[^2];
            var instance = parts[^1];
            if (!byInstance.TryGetValue(instance, out var entry))
                entry = (null, null);

            switch (column)
            {
                case "1" when long.TryParse(variable.Value, out var code):
                    entry = (code, entry.Severity);
                    break;
                case "2" when long.TryParse(variable.Value, out var severity):
                    entry = (entry.Code, severity);
                    break;
                default:
                    continue;
            }

            byInstance[instance] = entry;
        }

        foreach (var (code, severity) in byInstance.Values)
        {
            if (code is null || code.Value <= IdleAlertCodeMax)
                continue;
            yield return severity ?? 2; // unknown conta como pior-possível genérico
        }
    }

    internal static string MapSeverity(long severity) => severity switch
    {
        3 => "critical",
        4 => "major",
        5 => "minor",
        6 => "warning",
        7 => "informational",
        _ => "unknown"
    };
}
