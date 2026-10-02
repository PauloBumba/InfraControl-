using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Check SnmpSystem (ADR-014: default de Switch/Server/Router/Firewall/UPS).
/// Walk no system group MIB-II (1.3.6.1.2.1.1) pela mesma seam ISnmpClient do
/// discovery — identidade do device (sysDescr/sysName/sysUpTime) como prova de
/// vida SNMP. Sem provider aqui o ProviderRegistry.Resolve lançava a cada ciclo
/// de polling ("Check não suportado") e 5 tipos de device falhavam em vão.
/// </summary>
public sealed class SnmpSystemMonitoringProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>system group (MIB-II).</summary>
    public const string SystemGroupOid = "1.3.6.1.2.1.1";

    private const string SysDescrOid = "1.3.6.1.2.1.1.1.0";
    private const string SysUpTimeOid = "1.3.6.1.2.1.1.3.0";
    private const string SysNameOid = "1.3.6.1.2.1.1.5.0";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public string Name => CheckKind.SnmpSystem.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, CheckKind.SnmpSystem.ToString(), StringComparison.OrdinalIgnoreCase);

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
                target.Trim(), SystemGroupOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar SNMP: {exception.Message}");
        }

        stopwatch.Stop();

        string? sysDescr = null;
        string? sysUpTime = null;
        string? sysName = null;

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var oid = variable.Oid.TrimStart('.');
            if (oid == SysDescrOid)
                sysDescr = variable.Value;
            else if (oid == SysUpTimeOid)
                sysUpTime = variable.Value;
            else if (oid == SysNameOid)
                sysName = variable.Value;
        }

        if (sysDescr is null && sysUpTime is null && sysName is null)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Resposta SNMP sem o grupo system (sysDescr/sysUpTime/sysName).");

        var values = new Dictionary<string, string>();
        if (sysDescr is not null)
            values["sysDescr"] = sysDescr;
        if (sysUpTime is not null)
        {
            values["sysUpTime"] = sysUpTime;
            // Timeticks em centésimos de segundo (RFC 3411); parse best-effort
            // — formato "123456 (0:02:03.45)" fica só com o valor bruto.
            var digits = sysUpTime.Split(' ', 2)[0];
            if (long.TryParse(digits, out var ticks) && ticks >= 0)
                values["uptimeSeconds"] = (ticks / 100).ToString();
        }

        if (sysName is not null)
            values["sysName"] = sysName;

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }
}
