using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider SNMP de discovery (F4-T004 — escopo aprovado: só o system
/// group MIB-II, sem ifTable): walk de 1.3.6.1.2.1.1 via seam ISnmpClient
/// (mesma do monitoring, sem rede real nos testes). Falha/sem resposta é
/// achado negativo — o run é finalizado pelo comando, não aqui.
/// </summary>
public sealed class SnmpDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string SystemGroupOid = "1.3.6.1.2.1.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private static readonly (string Oid, string Key)[] SystemGroupMap =
    [
        ("1.3.6.1.2.1.1.1.0", "sys_descr"),
        ("1.3.6.1.2.1.1.3.0", "sys_up_time"),
        ("1.3.6.1.2.1.1.4.0", "sys_contact"),
        ("1.3.6.1.2.1.1.5.0", "sys_name"),
        ("1.3.6.1.2.1.1.6.0", "sys_location")
    ];

    public string Name => "SNMP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Snmp", StringComparison.OrdinalIgnoreCase);

    public async Task<DiscoveryProviderResult> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new DiscoveryProviderResult(
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do SNMP é obrigatório.",
                "Destino do SNMP é obrigatório.");

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, SystemGroupOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DiscoveryProviderResult(
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                $"Falha ao consultar SNMP: {exception.Message}",
                $"SNMP indisponível em {target}.");
        }

        if (variables.Count == 0)
            return new DiscoveryProviderResult(
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "SNMP sem resposta.",
                $"SNMP sem resposta em {target}.");

        var attributes = new Dictionary<string, string> { ["reachable"] = "true" };
        string? sysName = null;
        string? sysDescr = null;

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var oid = variable.Oid.TrimStart('.');
            var match = SystemGroupMap.FirstOrDefault(map => map.Oid == oid);
            if (match.Key is null)
                continue;

            attributes[match.Key] = variable.Value;
            if (match.Key == "sys_name")
                sysName = variable.Value;
            else if (match.Key == "sys_descr")
                sysDescr = variable.Value;
        }

        var summary = sysName is null
            ? $"SNMP: {target}."
            : Truncate(sysDescr is null
                ? $"SNMP: {sysName}."
                : $"SNMP: {sysName} — {sysDescr}");

        return new DiscoveryProviderResult(true, attributes, null, summary);
    }

    private static string Truncate(string summary)
        => summary.Length <= DiscoveryResult.SummaryMaxLength
            ? summary
            : summary[..DiscoveryResult.SummaryMaxLength];
}
