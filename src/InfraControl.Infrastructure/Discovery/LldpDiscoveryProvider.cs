using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider LLDP de discovery (F4-T005 — decisões do usuário: leitura via
/// SNMP/LLDP-MIB; walk sem vizinhos → registrar "nenhum"). Walk da
/// lldpRemTable (1.0.8802.1.1.2.1.4.1.1) agrupando colunas por índice de
/// vizinho (timeMark.localPort.remIndex) — um achado Kind=Neighbor por
/// vizinho. DeviceId do achado fica null até o T010 casar com device
/// conhecido.
/// </summary>
public sealed class LldpDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string RemTableOid = "1.0.8802.1.1.2.1.4.1.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    // Colunas da lldpRemEntry (IEEE 802.1AB).
    private static readonly Dictionary<string, string> ColumnMap = new()
    {
        ["2"] = "chassis_id_subtype",
        ["3"] = "chassis_id",
        ["4"] = "port_id_subtype",
        ["5"] = "port_id",
        ["6"] = "port_desc",
        ["7"] = "sys_name",
        ["8"] = "sys_descr"
    };

    public string Name => "LLDP";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Lldp", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino do LLDP é obrigatório.",
                "Destino do LLDP é obrigatório.")];

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, RemTableOid, DefaultTimeout, cancellationToken);
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
                $"Falha ao consultar LLDP: {exception.Message}",
                $"LLDP indisponível em {target}.")];
        }

        var groups = ParseGroups(variables);
        if (groups.Count == 0)
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                false,
                new Dictionary<string, string> { ["neighbors"] = "0" },
                "Nenhum vizinho LLDP.",
                "Nenhum vizinho LLDP.")];

        var findings = new List<DiscoveryProviderResult>(groups.Count);
        foreach (var group in groups.OrderBy(
            g => $"{g.Key.TimeMark}.{g.Key.LocalPort}.{g.Key.RemIndex}",
            StringComparer.Ordinal))
        {
            var fields = group.Value;
            fields.TryGetValue("sys_name", out var sysName);
            fields.TryGetValue("chassis_id", out var chassisId);
            fields.TryGetValue("port_id", out var portId);
            fields.TryGetValue("port_desc", out var portDesc);

            var name = sysName ?? chassisId ?? portId ?? "Vizinho desconhecido";
            var port = portDesc ?? portId;

            var attributes = new Dictionary<string, string>(fields)
            {
                ["local_port"] = group.Key.LocalPort,
                ["neighbors"] = groups.Count.ToString()
            };

            findings.Add(new DiscoveryProviderResult(
                DiscoveryResultKind.Neighbor,
                true,
                attributes,
                null,
                Truncate(string.IsNullOrEmpty(port) ? $"{name}." : $"{name} — {port}")));
        }

        return findings;
    }

    private readonly record struct NeighborIndex(
        string TimeMark, string LocalPort, string RemIndex);

    private static Dictionary<NeighborIndex, Dictionary<string, string>> ParseGroups(
        IReadOnlyList<SnmpVariable> variables)
    {
        var groups = new Dictionary<NeighborIndex, Dictionary<string, string>>();

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            var prefixLength = RemTableOid.Split('.').Length;
            // ...RemTable.<col>.<timeMark>.<localPort>.<remIndex>
            if (parts.Length != prefixLength + 4)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    RemTableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            if (!ColumnMap.TryGetValue(column, out var key))
                continue;

            var index = new NeighborIndex(
                parts[prefixLength + 1],
                parts[prefixLength + 2],
                parts[prefixLength + 3]);

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
