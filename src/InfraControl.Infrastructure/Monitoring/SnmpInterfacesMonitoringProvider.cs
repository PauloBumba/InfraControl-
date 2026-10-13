using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Check SnmpInterfaces (F29-3): saúde agregada das interfaces de um device.
/// Walk na <c>ifTable</c> MIB-II (1.3.6.1.2.1.2.2.1 — ifOperStatus/ifInErrors/
/// ifOutErrors) e na <c>ifXTable</c> (1.3.6.1.2.1.31.1.1.1 — ifHCInOctets/
/// ifHCOutOctets), correlacionados por ifIndex. Reporta um agregado por
/// device (interfaceCount, upCount, totais de octets/erros). Disponível mas
/// não é default de nenhum tipo — habilitar por device quando o SNMP
/// community estiver liberado (gate da F29-3). REGRA: só afirma "down" o que
/// o device respondeu; octets = 0 quando o ifXTable não veio (dispositivo
/// sem MIB-II estendida) — nunca inventado.
/// </summary>
public sealed class SnmpInterfacesMonitoringProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>ifTable (MIB-II) — status e erros.</summary>
    public const string IfTableOid = "1.3.6.1.2.1.2.2.1";

    /// <summary>ifXTable (MIB-II extensa) — contadores HC de octets.</summary>
    public const string IfXTableOid = "1.3.6.1.2.1.31.1.1.1";

    // ifTable columns
    private const string IfOperStatusOid = "1.3.6.1.2.1.2.2.1.8";
    private const string IfInErrorsOid = "1.3.6.1.2.1.2.2.1.14";
    private const string IfOutErrorsOid = "1.3.6.1.2.1.2.2.1.20";

    // ifXTable columns
    private const string IfHcInOctetsOid = "1.3.6.1.2.1.31.1.1.1.6";
    private const string IfHcOutOctetsOid = "1.3.6.1.2.1.31.1.1.1.10";

    /// <summary>RFC 2863 ifOperStatus: 1 = up.</summary>
    private const string OperStatusUp = "1";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpInterfaces.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, CheckKind.SnmpInterfaces.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();

        // ifTable é essencial (MIB-II base) — sem ele o device não fala SNMP
        // de interfaces; falha aqui = check falha.
        IReadOnlyList<SnmpVariable> ifTable;
        try
        {
            ifTable = await snmpClient.WalkAsync(
                target.Trim(), IfTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar ifTable: {exception.Message}");
        }

        // ifXTable é opcional (alguns devices não têm) — falha só zera octets.
        IReadOnlyList<SnmpVariable> ifXTable = [];
        try
        {
            ifXTable = await snmpClient.WalkAsync(
                target.Trim(), IfXTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // ifXTable indisponível — octets ficam 0 (nunca inventados).
        }

        stopwatch.Stop();

        // ifIndex → acumulado (status/erros de ifTable + octets de ifXTable)
        var byIndex = new Dictionary<int, long[]>();
        // [0]=operStatus, [1]=inErrors, [2]=outErrors, [3]=inOctets, [4]=outOctets

        long[] Entry(int index) =>
            byIndex.TryGetValue(index, out var values)
                ? values
                : byIndex[index] = new long[5];

        foreach (var variable in ifTable)
        {
            var oid = variable.Oid.TrimStart('.');
            var index = TryParseIndex(oid, IfOperStatusOid);
            if (index is not null)
            {
                Entry(index.Value)[0] = long.TryParse(variable.Value, out var status) ? status : 0;
                continue;
            }
            index = TryParseIndex(oid, IfInErrorsOid);
            if (index is not null)
            {
                Entry(index.Value)[1] = ParseCounter(variable.Value);
                continue;
            }
            index = TryParseIndex(oid, IfOutErrorsOid);
            if (index is not null)
            {
                Entry(index.Value)[2] = ParseCounter(variable.Value);
            }
        }

        foreach (var variable in ifXTable)
        {
            var oid = variable.Oid.TrimStart('.');
            var index = TryParseIndex(oid, IfHcInOctetsOid);
            if (index is not null)
            {
                Entry(index.Value)[3] = ParseCounter(variable.Value);
                continue;
            }
            index = TryParseIndex(oid, IfHcOutOctetsOid);
            if (index is not null)
            {
                Entry(index.Value)[4] = ParseCounter(variable.Value);
            }
        }

        if (byIndex.Count == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Resposta SNMP sem interfaces na ifTable (ifOperStatus/ifInErrors).");

        var upCount = byIndex.Values.Count(v => v[0] == 1);
        var totalInErrors = byIndex.Values.Sum(v => v[1]);
        var totalOutErrors = byIndex.Values.Sum(v => v[2]);
        var totalInOctets = byIndex.Values.Sum(v => v[3]);
        var totalOutOctets = byIndex.Values.Sum(v => v[4]);

        var values = new Dictionary<string, string>
        {
            ["interfaceCount"] = byIndex.Count.ToString(),
            ["upCount"] = upCount.ToString(),
            ["totalInErrors"] = totalInErrors.ToString(),
            ["totalOutErrors"] = totalOutErrors.ToString(),
            ["totalInOctets"] = totalInOctets.ToString(),
            ["totalOutOctets"] = totalOutOctets.ToString()
        };

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>
    /// Extrai o ifIndex de um OID no formato <c>&lt;columnOid&gt;.&lt;index&gt;</c>
    /// (devolve null se o OID não está sob a coluna).
    /// </summary>
    private static int? TryParseIndex(string oid, string columnOid)
    {
        if (!oid.StartsWith(columnOid + ".", StringComparison.Ordinal))
            return null;

        var suffix = oid[(columnOid.Length + 1)..];
        return int.TryParse(suffix, out var index) && index >= 0 ? index : null;
    }

    /// <summary>Contador SNMP: best-effort, negativo/inválido vira 0 (nunca inventa).</summary>
    private static long ParseCounter(string value)
        => long.TryParse(value.Trim(), out var parsed) && parsed >= 0 ? parsed : 0;
}
