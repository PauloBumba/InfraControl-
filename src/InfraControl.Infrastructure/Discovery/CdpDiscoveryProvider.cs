using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider CDP de discovery (F4-T006) — CISCO-CDP-MIB walk da
/// cdpCacheTable (1.3.6.1.4.1.9.9.23.1.2.1.1) agrupada por índice
/// ifIndex.deviceIndex, mesmo padrão do LLDP (F4-T005): um achado
/// Kind=Neighbor por vizinho; walk vazio registra "nenhum". OIDs das
/// colunas: .4 address, .5 version, .6 device_id, .7 device_port.
/// </summary>
public sealed class CdpDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string CacheTableOid = "1.3.6.1.4.1.9.9.23.1.2.1.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, string> ColumnMap = new()
    {
        ["4"] = "address",
        ["5"] = "version",
        ["6"] = "device_id",
        ["7"] = "device_port"
    };

    public string Name => "CDP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Cdp", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do CDP é obrigatório.",
                "Destino do CDP é obrigatório.")];

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, CacheTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                $"Falha ao consultar CDP: {exception.Message}",
                $"CDP indisponível em {target}.")];
        }

        var groups = ParseGroups(variables);
        if (groups.Count == 0)
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                false,
                new Dictionary<string, string> { ["neighbors"] = "0" },
                "Nenhum vizinho CDP.",
                "Nenhum vizinho CDP.")];

        var findings = new List<DiscoveryProviderResult>(groups.Count);
        foreach (var group in groups.OrderBy(
            g => $"{g.Key.IfIndex}.{g.Key.DeviceIndex}", StringComparer.Ordinal))
        {
            var fields = group.Value;
            fields.TryGetValue("device_id", out var deviceId);
            fields.TryGetValue("device_port", out var devicePort);

            var name = deviceId
                ?? fields.GetValueOrDefault("address")
                ?? "Vizinho desconhecido";

            var attributes = new Dictionary<string, string>(fields)
            {
                ["local_if"] = group.Key.IfIndex,
                ["neighbors"] = groups.Count.ToString()
            };

            findings.Add(new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                true,
                attributes,
                null,
                Truncate(string.IsNullOrEmpty(devicePort)
                    ? $"{name}."
                    : $"{name} — {devicePort}")));
        }

        return findings;
    }

    private readonly record struct CacheIndex(string IfIndex, string DeviceIndex);

    private static Dictionary<CacheIndex, Dictionary<string, string>> ParseGroups(
        IReadOnlyList<SnmpVariable> variables)
    {
        var groups = new Dictionary<CacheIndex, Dictionary<string, string>>();

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            var prefixLength = CacheTableOid.Split('.').Length;
            // ...CacheTable.<col>.<ifIndex>.<deviceIndex>
            if (parts.Length != prefixLength + 3)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    CacheTableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            if (!ColumnMap.TryGetValue(column, out var key))
                continue;

            var index = new CacheIndex(parts[prefixLength + 1], parts[prefixLength + 2]);
            if (!groups.TryGetValue(index, out var fields))
                groups[index] = fields = new Dictionary<string, string>();

            fields[key] = variable.Value;
        }

        return groups;
    }

    private static string Truncate(string summary)
        => summary.Length <= DiscoveryResult.SummaryMaxLength
            ? summary
            : summary[..DiscoveryResult.SummaryMaxLength];
}
