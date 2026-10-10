using InfraControl.Application.Network.Services;
using InfraControl.Application.Network.Services.Ports;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.NetworkServices.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.NetworkServices;

/// <summary>
/// Leitura dos serviços de rede (F18-T005 · ADR-042) — SOMENTE LEITURA:
/// só walk SNMPv2c (GET) e um comando show por vendor. Leases vêm de
/// <c>ipNetToMediaTable/ipNetToPhysicalTable</c> (<c>1.3.6.1.2.1.4.22.1</c>
/// — colunas .1 ifIndex/.2 physAddress/.3 netAddress são iguais nas duas
/// variantes do RFC1213/RFC4293) e rotas de <c>ipRouteTable</c>
/// (<c>1.3.6.1.2.1.4.21.1</c>, RFC1213). Sem target configurado ou
/// equipamento inalcançável → <see cref="InvalidOperationException"/>
/// com motivo pt-BR (a API responde 503 + motivo).
/// </summary>
public sealed class NetworkServicesReader(
    ISnmpClient snmp,
    ISshCommandRunner ssh,
    IFirewallVendorReaderSelector firewallReaders,
    IOptions<NetworkServicesOptions> options)
    : INetworkServicesReader
{
    public const string ArpTableOid = "1.3.6.1.2.1.4.22.1";
    public const string RouteTableOid = "1.3.6.1.2.1.4.21.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<DhcpLeaseDto>> GetDhcpLeasesAsync(
        CancellationToken cancellationToken = default)
    {
        var variables = await WalkAsync(ArpTableOid, cancellationToken);

        // ipNetToMedia/ipNetToPhysical: .1 ifIndex · .2 physAddress · .3 netAddress.
        var leases = new List<DhcpLeaseDto>();
        foreach (var fields in GroupByIndex(variables, ArpTableOid))
        {
            var ip = fields.GetValueOrDefault("3") ?? string.Empty;
            if (ip.Length == 0)
                continue; // sem IP a entrada não identifica um lease

            leases.Add(new DhcpLeaseDto(
                ip,
                NormalizeMac(fields.GetValueOrDefault("2") ?? string.Empty),
                ParseInt(fields.GetValueOrDefault("1"))));
        }

        return leases
            .OrderBy(lease => lease.Ip, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<NetworkRouteDto>> GetRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        var variables = await WalkAsync(RouteTableOid, cancellationToken);

        // ipRouteTable (RFC1213): .1 dest · .2 ifIndex · .3 nextHop ·
        // .4 metric · .10 type · .11 proto · .13 mask.
        var routes = new List<NetworkRouteDto>();
        foreach (var fields in GroupByIndex(variables, RouteTableOid))
        {
            var destination = fields.GetValueOrDefault("1") ?? string.Empty;
            if (destination.Length == 0)
                continue;

            routes.Add(new NetworkRouteDto(
                destination,
                fields.GetValueOrDefault("13") ?? string.Empty,
                fields.GetValueOrDefault("3") ?? string.Empty,
                ParseInt(fields.GetValueOrDefault("2")),
                ParseInt(fields.GetValueOrDefault("4")),
                RouteTypeLabel(fields.GetValueOrDefault("10") ?? string.Empty),
                RouteProtocolLabel(fields.GetValueOrDefault("11") ?? string.Empty)));
        }

        return routes
            .OrderBy(route => route.Destination, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<FirewallRuleDto>> GetFirewallRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.IsConfigured)
            throw NotConfigured();

        // Vendor primeiro: desconhecido falha antes de abrir sessão SSH.
        var reader = firewallReaders.Resolve(config.Vendor);
        var output = await ssh.ExecuteAsync(reader.Command, cancellationToken);
        return reader.Parse(output);
    }

    private async Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string oid, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.IsConfigured)
            throw NotConfigured();

        try
        {
            return await snmp.WalkAsync(
                config.Target, oid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Falha ao consultar {config.Target} via SNMP ({oid}): {exception.Message}",
                exception);
        }
    }

    private static InvalidOperationException NotConfigured()
        => new("Serviços de rede não configurados (NetworkServices:Target).");

    /// <summary>Agrupa as variáveis da tabela por índice (sub-ids após a
    /// coluna) → mapa coluna → valor, como no DhcpDiscoveryProvider.</summary>
    private static IReadOnlyList<Dictionary<string, string>> GroupByIndex(
        IReadOnlyList<SnmpVariable> variables, string tableOid)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var prefixLength = tableOid.Split('.').Length;

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Value))
                continue;

            var parts = variable.Oid.TrimStart('.').Split('.');
            if (parts.Length <= prefixLength + 1)
                continue;
            if (!string.Join('.', parts[..prefixLength]).Equals(
                    tableOid, StringComparison.Ordinal))
                continue;

            var column = parts[prefixLength];
            var index = string.Join('.', parts[(prefixLength + 1)..]);

            if (!groups.TryGetValue(index, out var fields))
                groups[index] = fields = new Dictionary<string, string>();

            fields[column] = variable.Value;
        }

        return groups.Values.ToArray();
    }

    /// <summary>MAC do device: hex de 12 dígitos → aa:bb:cc:dd:ee:ff;
    /// qualquer outro formato fica cru (nunca reinterpretado).</summary>
    internal static string NormalizeMac(string raw)
    {
        var hex = new string(raw.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length != 12)
            return raw;

        hex = hex.ToLowerInvariant();
        return string.Join(':', Enumerable.Range(0, 6)
            .Select(octet => hex.Substring(octet * 2, 2)));
    }

    private static int ParseInt(string raw)
        => int.TryParse(raw, out var value) ? value : 0;

    /// <summary>ipRouteType (RFC1213): other/direct/indirect.</summary>
    private static string RouteTypeLabel(string raw)
        => raw switch
        {
            "1" => "other",
            "2" => "direct",
            "3" => "indirect",
            _ => raw // número fora do mapa = cru, sem rótulo inventado
        };

    /// <summary>ipRouteProto (RFC1213): other/local/icmp/egp/ggp/hello/
    /// rip/isis/igrp/dvmrp/ospf/native…</summary>
    private static string RouteProtocolLabel(string raw)
        => raw switch
        {
            "1" => "other",
            "2" => "local",
            "3" => "icmp",
            "4" => "egp",
            "5" => "ggp",
            "6" => "hello",
            "7" => "rip",
            "8" => "isis",
            "9" => "egp",
            "10" => "igrp",
            "11" => "dvmrp",
            "12" => "ospf",
            "13" => "native",
            "14" => "bipolar-ex",
            _ => raw
        };
}
