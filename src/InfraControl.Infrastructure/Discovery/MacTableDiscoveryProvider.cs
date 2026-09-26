using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Provider MAC Tables de discovery (F4-T009 — decisões aprovadas:
/// filtro só status=3 learned e chave de provider "Mac"): walk da
/// dot1dTpFdbTable (BRIDGE-MIB RFC 4188, 1.3.6.1.2.1.17.4.3) indexada
/// por MAC (6 octetos), colunas .1 address / .2 port / .3 status.
/// Um achado Kind=MacTableEntry por entrada learned; tabela vazia ou
/// só não-learned registra "nenhuma".
/// </summary>
public sealed class MacTableDiscoveryProvider(ISnmpClient snmpClient) : IDiscoveryProvider
{
    public const string FdbTableOid = "1.3.6.1.2.1.17.4.3";
    public const string LearnedStatus = "3";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, string> ColumnMap = new()
    {
        ["1"] = "mac",
        ["2"] = "bridge_port",
        ["3"] = "fdb_status"
    };

    public string Name => "MAC";

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, "Mac", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.MacTableEntry,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                "Destino da MAC é obrigatório.",
                "Destino da MAC é obrigatório.")];

        target = target.Trim();

        IReadOnlyList<SnmpVariable> variables;
        try
        {
            variables = await snmpClient.WalkAsync(
                target, FdbTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.MacTableEntry,
                false,
                new Dictionary<string, string> { ["reachable"] = "false" },
                $"Falha ao consultar MAC: {exception.Message}",
                $"MAC indisponível em {target}.")];
        }

        var groups = ParseGroups(variables);
        // Só entradas learned(3) com mac identificam a MAC table (decisão
        // aprovada); self/invalid/other são descartadas.
        var usable = groups
            .Where(group => group.Value.ContainsKey("mac")
                && group.Value.TryGetValue("fdb_status", out var status)
                && status == LearnedStatus)
            .OrderBy(group => group.Value["mac"], StringComparer.Ordinal)
            .ToArray();

        if (usable.Length == 0)
            return [new DiscoveryProviderResult(
                DiscoveryResultKind.MacTableEntry,
                false,
                new Dictionary<string, string> { ["mac_entries"] = "0" },
                "Nenhuma entrada MAC.",
                "Nenhuma entrada MAC.")];

        var findings = new List<DiscoveryProviderResult>(usable.Length);
        foreach (var group in usable)
        {
            var fields = group.Value;
            var mac = fields["mac"];
            fields.TryGetValue("bridge_port", out var port);

            var attributes = new Dictionary<string, string>(fields)
            {
                ["mac_entries"] = usable.Length.ToString()
            };

            findings.Add(new DiscoveryProviderResult(
                DiscoveryResultKind.MacTableEntry,
                true,
                attributes,
                null,
                Truncate(string.IsNullOrEmpty(port) ? $"{mac}." : $"{mac} — porta {port}")));
        }

        return findings;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseGroups(
        IReadOnlyList<SnmpVariable> variables)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var prefixLength = FdbTableOid.Split('.').Length;

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            // ...FdbTable.<col>.<mac 6 octetos> = prefixo + 7
            if (parts.Length < prefixLength + 2)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    FdbTableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            if (!ColumnMap.TryGetValue(column, out var key))
                continue;

            // Índice: MAC em octetos (sub-ids após a coluna).
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
