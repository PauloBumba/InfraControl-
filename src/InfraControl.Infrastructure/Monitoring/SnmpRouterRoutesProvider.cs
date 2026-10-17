using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T007a: check SnmpRouterRoutes — tabela de rotas IPv4 via
/// <c>ipCidrRouteTable</c> (IP-FORWARD-MIB RFC 2096, universal). As linhas
/// são agrupadas por instância (dest.mask.tos.nextHop); classifica-se o
/// protocolo: local/netmgmt = estática, ospf/bgp/igrp/eigrp = dinâmica,
/// demais = other. Rota padrão (dest 0.0.0.0) expõe o <c>defaultGateway</c>.
/// Informativo por design — sem regra no AlertEvaluator (limiar de rota é
/// específico de modelo; capacidade é coberta pelos checks de saúde).
/// Opt-in — não é default de Router/Firewall.
/// </summary>
public sealed class SnmpRouterRoutesProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>ipCidrRouteTable (colunas dest/nextHop/type/proto/…).</summary>
    public const string RouteTableOid = "1.3.6.1.2.1.4.24.4.1";

    // colunas da ipCidrRouteTable (RFC 2096)
    private const string ColDest = "1";
    private const string ColNextHop = "2";
    private const string ColType = "4";
    private const string ColProto = "5";

    // ipCidrRouteProto (RFC 2096) — classificação estática × dinâmica.
    private const string ProtoLocal = "2";
    private const string ProtoNetmgmt = "3";
    private static readonly HashSet<string> DynamicProtos = ["10", "14", "15", "19"];

    // ipCidrRouteType (RFC 2096): other(1) invalid(2) local(3) indirect(4).
    private const string TypeInvalid = "2";

    private const int TopRoutes = 20;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpRouterRoutes.ToString();

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
        IReadOnlyList<SnmpVariable> rows;
        try
        {
            rows = await snmpClient.WalkAsync(
                target.Trim(), RouteTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar SNMP: {ex.Message}");
        }

        var routes = GroupByInstance(rows)
            .Select(pair => ToRoute(pair.Key, pair.Value))
            .Where(route => route is not null)
            .Select(route => route!)
            .Where(route => route.Type != TypeInvalid)
            .OrderBy(route => route.Dest, StringComparer.Ordinal)
            .ToArray();

        stopwatch.Stop();

        if (routes.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhuma rota IPv4 válida encontrada no router/firewall.");

        var defaultRoute = routes.FirstOrDefault(route => IsDefaultRoute(route.Dest));
        var staticCount = routes.Count(route => route.Proto is ProtoLocal or ProtoNetmgmt);
        var dynamicCount = routes.Count(route => DynamicProtos.Contains(route.Proto));

        var values = new Dictionary<string, string>
        {
            ["routeCount"] = routes.Length.ToString(),
            ["defaultGateway"] = defaultRoute?.NextHop ?? string.Empty,
            ["staticCount"] = staticCount.ToString(),
            ["dynamicCount"] = dynamicCount.ToString(),
            ["routes"] = JsonSerializer.Serialize(routes.Take(TopRoutes).Select(route => new
            {
                dest = route.Dest,
                nextHop = route.NextHop,
                proto = ProtoLabel(route.Proto),
                type = route.Type
            }))
        };

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    private sealed record RouteInfo(string Dest, string NextHop, string Proto, string Type);

    private static Dictionary<string, RouteEntry> GroupByInstance(
        IReadOnlyList<SnmpVariable> rows)
    {
        // O INDEX da ipCidrRouteTable é composto (dest+mask+tos+nextHop =
        // 13 sub-identificadores); a coluna fica em posição FIXA logo após
        // o base OID (10 sub-ids: 1.3.6.1.2.1.4.24.4.1) e a instância é o
        // resto do OID (não é o último sub-id isolado).
        var baseLength = RouteTableOid.Split('.').Length;
        var byInstance = new Dictionary<string, RouteEntry>(StringComparer.Ordinal);
        foreach (var variable in rows)
        {
            var parts = variable.Oid.TrimEnd('.').Split('.');
            if (parts.Length < baseLength + 2)
                continue;

            var column = parts[baseLength];
            var instance = string.Join('.', parts.AsSpan(baseLength + 1));
            if (!byInstance.TryGetValue(instance, out var entry))
                entry = new RouteEntry(null, null, null, null);

            entry = column switch
            {
                ColDest => entry with { Dest = variable.Value },
                ColNextHop => entry with { NextHop = variable.Value },
                ColType => entry with { Type = variable.Value },
                ColProto => entry with { Proto = variable.Value },
                _ => entry
            };
            byInstance[instance] = entry;
        }

        return byInstance;
    }

    private static RouteInfo? ToRoute(string instance, RouteEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Dest) || string.IsNullOrWhiteSpace(entry.NextHop))
            return null;

        return new RouteInfo(
            entry.Dest,
            entry.NextHop,
            string.IsNullOrWhiteSpace(entry.Proto) ? "1" : entry.Proto,
            string.IsNullOrWhiteSpace(entry.Type) ? "1" : entry.Type);
    }

    private static bool IsDefaultRoute(string dest)
        => dest == "0.0.0.0" || dest == "0.0.0.0/0";

    private static string ProtoLabel(string proto) => proto switch
    {
        "1" => "other",
        "2" => "local",
        "3" => "netmgmt",
        "4" => "icmp",
        "10" => "igrp",
        "14" => "ospf",
        "15" => "bgp",
        "19" => "eigrp",
        _ => $"proto-{proto}"
    };

    private sealed record RouteEntry(string? Dest, string? NextHop, string? Proto, string? Type);
}
