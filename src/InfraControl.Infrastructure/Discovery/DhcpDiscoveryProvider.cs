using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider DHCP de discovery (F4-T008 — escopo aprovado: bindings de DHCP
/// snooping, vendor Cisco): walk da cdsBindingsTable (CISCO-DHCP-SNOOPING-MIB
/// 1.3.6.1.4.1.9.9.380.1.4.1) indexada por vlan + mac (octetos), colunas
/// .1 vlan / .2 mac / .3 addrType / .4 ip / .5 interface / .6 leasedTime /
/// .7 status / .8 hostname. Um achado Kind=DhcpLease por binding; binding
/// sem ip é descartado; tabela vazia registra "nenhuma".
/// </summary>
public sealed class DhcpDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string BindingsTableOid = "1.3.6.1.4.1.9.9.380.1.4.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, string> ColumnMap = new()
    {
        ["1"] = "vlan",
        ["2"] = "mac",
        ["3"] = "addr_type",
        ["4"] = "ip",
        ["5"] = "local_if",
        ["6"] = "leased_time",
        ["7"] = "binding_status",
        ["8"] = "hostname"
    };

    public string Name => "DHCP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Dhcp", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.DhcpLease,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do DHCP é obrigatório.",
                "Destino do DHCP é obrigatório.")];

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, BindingsTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.DhcpLease,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                $"Falha ao consultar DHCP: {exception.Message}",
                $"DHCP indisponível em {target}.")];
        }

        var groups = ParseGroups(variables);
        // Binding sem ip não identifica o lease DHCP — descartado.
        var usable = groups
            .Where(group => group.Value.ContainsKey("ip"))
            .OrderBy(group => group.Value["ip"], StringComparer.Ordinal)
            .ToArray();

        if (usable.Length == 0)
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.DhcpLease,
                false,
                new Dictionary<string, string> { ["dhcp_bindings"] = "0" },
                "Nenhuma entrada DHCP.",
                "Nenhuma entrada DHCP.")];

        var findings = new List<DiscoveryProviderResult>(usable.Length);
        foreach (var group in usable)
        {
            var fields = group.Value;
            var ip = fields["ip"];
            fields.TryGetValue("mac", out var mac);

            var attributes = new Dictionary<string, string>(fields)
            {
                ["dhcp_bindings"] = usable.Length.ToString()
            };

            findings.Add(new DiscoveryProviderResult(
                DiscoveryResultKind.DhcpLease,
                true,
                attributes,
                null,
                Truncate(string.IsNullOrEmpty(mac) ? $"{ip}." : $"{ip} — {mac}")));
        }

        return findings;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseGroups(
        IReadOnlyList<SnmpVariable> variables)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var prefixLength = BindingsTableOid.Split('.').Length;

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            // ...BindingsTable.<col>.<vlan>.<mac 6 octetos> = prefixo + 8
            if (parts.Length < prefixLength + 2)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    BindingsTableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            if (!ColumnMap.TryGetValue(column, out var key))
                continue;

            // Índice: vlan + mac em octetos (sub-ids após a coluna).
            var index = string.Join('.', parts[(prefixLength + 1)..]);

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
