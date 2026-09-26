using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider ARP de discovery (F4-T007 — decisões aprovadas: 1 achado por
/// entrada, sem filtro de tipo): walk do ipNetToMediaTable (IP-MIB
/// 1.3.6.1.2.1.4.22.1) agrupada por índice ifIndex.a.b.c.d, colunas
/// .1 ifIndex / .2 physAddress / .3 netAddress / .4 type. Um achado
/// Kind=ArpEntry por entrada; tabela vazia registra "nenhuma".
/// </summary>
public sealed class ArpDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string MediaTableOid = "1.3.6.1.2.1.4.22.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, string> ColumnMap = new()
    {
        ["1"] = "if_index",
        ["2"] = "mac",
        ["3"] = "ip",
        ["4"] = "arp_type"
    };

    public string Name => "ARP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Arp", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.ArpEntry,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do ARP é obrigatório.",
                "Destino do ARP é obrigatório.")];

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, MediaTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.ArpEntry,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                $"Falha ao consultar ARP: {exception.Message}",
                $"ARP indisponível em {target}.")];
        }

        var groups = ParseGroups(variables);
        // Entrada sem netAddress não identifica o fato ARP — descartada.
        var usable = groups
            .Where(group => group.Value.ContainsKey("ip"))
            .OrderBy(group => group.Value["ip"], StringComparer.Ordinal)
            .ToArray();

        if (usable.Length == 0)
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.ArpEntry,
                false,
                new Dictionary<string, string> { ["arp_entries"] = "0" },
                "Nenhuma entrada ARP.",
                "Nenhuma entrada ARP.")];

        var findings = new List<DiscoveryProviderResult>(usable.Length);
        foreach (var group in usable)
        {
            var fields = group.Value;
            var ip = fields["ip"];
            fields.TryGetValue("mac", out var mac);

            var attributes = new Dictionary<string, string>(fields)
            {
                ["local_if"] = group.Key.IfIndex,
                ["arp_entries"] = usable.Length.ToString()
            };

            findings.Add(new DiscoveryProviderResult(
                DiscoveryResultKind.ArpEntry,
                true,
                attributes,
                null,
                Truncate(string.IsNullOrEmpty(mac) ? $"{ip}." : $"{ip} — {mac}")));
        }

        return findings;
    }

    private readonly record struct ArpIndex(string IfIndex, string Address);

    private static Dictionary<ArpIndex, Dictionary<string, string>> ParseGroups(
        IReadOnlyList<SnmpVariable> variables)
    {
        var groups = new Dictionary<ArpIndex, Dictionary<string, string>>();

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            var prefixLength = MediaTableOid.Split('.').Length;
            // ...MediaTable.<col>.<ifIndex>.<a>.<b>.<c>.<d> = prefixo + 6
            if (parts.Length != prefixLength + 6)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    MediaTableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            if (!ColumnMap.TryGetValue(column, out var key))
                continue;

            var index = new ArpIndex(
                parts[prefixLength + 1],
                string.Join('.', parts[(prefixLength + 2)..]));

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
