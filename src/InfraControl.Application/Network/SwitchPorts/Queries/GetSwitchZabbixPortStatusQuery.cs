using System.Globalization;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.SwitchPorts.Queries;

// Status ao vivo das portas de um switch monitorado pelo Zabbix (SNMP).
// Resolve referência Zabbix → hostId → itens net.if.* (LLD ifTable dos
// templates HP Comware / Network Generic) → último valor de
// net.if.status (ifOperStatus) + taxa de bytes/s de net.if.in/out
// calculada dos dois últimos pontos do history.get em lote.
// Sem referência/Zabbix → lista vazia (não é erro). Cache curto: o front
// faz polling e a API do Zabbix não deve ser martelada.

/// <summary>Uma porta/interface do switch vista pelo Zabbix.</summary>
public sealed record ZabbixPortStatusDto(
    string InterfaceName,
    int? IfIndex,
    bool? LinkUp,
    double? SpeedMbps,
    string? Duplex,
    double? InBytesPerSecond,
    double? OutBytesPerSecond);

public sealed record GetSwitchZabbixPortStatusQuery(Guid SwitchId)
    : BaseQuery<IReadOnlyList<ZabbixPortStatusDto>>, IAuthorizedRequest
{
    public override string CacheKey => $"{GetType().Name}:{SwitchId}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetSwitchZabbixPortStatusQueryHandler(
    GetSwitchQueryHandler switchQueryHandler,
    IExternalDeviceReferenceRepository referenceRepository,
    IZabbixHostsRepository zabbixHostsRepository,
    IZabbixClient zabbixClient)
    : IRequestHandler<GetSwitchZabbixPortStatusQuery, IReadOnlyList<ZabbixPortStatusDto>>
{
    // Prefixos exatos com '[' — "net.if.in[" NÃO casa "net.if.in.errors[".
    private const string StatusPrefix = "net.if.status[";
    private const string InPrefix = "net.if.in[";
    private const string OutPrefix = "net.if.out[";
    private const string SpeedPrefix = "net.if.speed[";
    private const string DuplexPrefix = "net.if.duplex[";

    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(15);
    private const int HistoryLimit = 20000;

    public async Task<IReadOnlyList<ZabbixPortStatusDto>> Handle(
        GetSwitchZabbixPortStatusQuery request, CancellationToken cancellationToken)
    {
        await switchQueryHandler.Handle(
            new GetSwitchQuery(request.SwitchId), cancellationToken);

        if (!zabbixClient.IsConfigured)
            return [];

        var references = await referenceRepository.ListByDeviceIdAsync(
            request.SwitchId, cancellationToken);
        var zabbixRef = references.FirstOrDefault(r => r.Source == ExternalDeviceSource.Zabbix);
        if (zabbixRef is null)
            return [];

        var host = await zabbixHostsRepository.GetByHostIdAsync(
            zabbixRef.ExternalId, cancellationToken);
        if (host is null || host.MissingSinceUtc is not null)
            return [];

        var items = await zabbixClient.ListItemsAsync(host.HostId, cancellationToken);

        var statusItems = items
            .Where(i => !i.Unsupported && i.Key.StartsWith(StatusPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var inItems = items
            .Where(i => !i.Unsupported && i.Key.StartsWith(InPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var outItems = items
            .Where(i => !i.Unsupported && i.Key.StartsWith(OutPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var speedItems = items
            .Where(i => !i.Unsupported && i.Key.StartsWith(SpeedPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var duplexItems = items
            .Where(i => !i.Unsupported && i.Key.StartsWith(DuplexPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (statusItems.Count == 0 && inItems.Count == 0 && outItems.Count == 0 &&
            speedItems.Count == 0 && duplexItems.Count == 0)
            return [];

        var lastValues = await zabbixClient.GetItemsLastValuesAsync(
            host.HostId, cancellationToken);
        var lastByItem = lastValues
            .GroupBy(v => v.ItemId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var counterIds = inItems.Concat(outItems).Select(i => i.ItemId).ToList();
        var rates = await GetRatesAsync(counterIds, cancellationToken);

        var ports = new Dictionary<string, ZabbixPortStatusDto>(StringComparer.OrdinalIgnoreCase);

        void Merge(ZabbixItemDto item, string kind)
        {
            var ifIndex = ExtractIfIndex(item.Key);
            var name = ExtractDisplayName(item.Name, item.Key);
            var key = $"{ifIndex}|{name}";
            if (!ports.TryGetValue(key, out var port))
            {
                port = new ZabbixPortStatusDto(name, ifIndex, null, null, null, null, null);
                ports[key] = port;
            }

            if (kind == "status" && lastByItem.TryGetValue(item.ItemId, out var lv))
            {
                port = port with { LinkUp = InterpretOperStatus(lv.LastValue) };
            }
            else if (kind == "speed" && lastByItem.TryGetValue(item.ItemId, out var sv) &&
                     double.TryParse(sv.LastValue, NumberStyles.Float,
                         CultureInfo.InvariantCulture, out var speed) && speed > 0)
            {
                port = port with { SpeedMbps = speed };
            }
            else if (kind == "duplex" && lastByItem.TryGetValue(item.ItemId, out var dv))
            {
                port = port with { Duplex = InterpretDuplex(dv.LastValue) };
            }
            else if (kind == "in" && rates.TryGetValue(item.ItemId, out var inRate))
            {
                port = port with { InBytesPerSecond = inRate };
            }
            else if (kind == "out" && rates.TryGetValue(item.ItemId, out var outRate))
            {
                port = port with { OutBytesPerSecond = outRate };
            }
            ports[key] = port;
        }

        foreach (var item in statusItems) Merge(item, "status");
        foreach (var item in speedItems) Merge(item, "speed");
        foreach (var item in duplexItems) Merge(item, "duplex");
        foreach (var item in inItems) Merge(item, "in");
        foreach (var item in outItems) Merge(item, "out");

        return ports.Values
            .OrderBy(p => p.IfIndex ?? int.MaxValue)
            .ThenBy(p => p.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Taxa (bytes/s) dos contadores: dois últimos pontos do histórico
    /// dentro da janela. Consulta history=3 (unsigned) e history=0
    /// (float) — cobre os dois value_types que os templates usam.
    /// </summary>
    private async Task<Dictionary<string, double>> GetRatesAsync(
        IReadOnlyList<string> itemIds, CancellationToken cancellationToken)
    {
        var rates = new Dictionary<string, double>(StringComparer.Ordinal);
        if (itemIds.Count == 0)
            return rates;

        var to = DateTime.UtcNow;
        var from = to - HistoryWindow;

        var points = new List<ZabbixHistoryValueDto>();
        foreach (var history in new[] { 3, 0 })
        {
            points.AddRange(await zabbixClient.GetHistoryBatchAsync(
                itemIds, history, from, to, HistoryLimit, cancellationToken));
        }

        // history=3 e history=0 são tabelas distintas, mas um item só existe
        // em uma delas — dedup por (item, clock) blinda contra sobreposição.
        points = points
            .GroupBy(p => (p.ItemId, p.ClockUtc))
            .Select(g => g.First())
            .ToList();

        foreach (var group in points.GroupBy(p => p.ItemId, StringComparer.Ordinal))
        {
            var ordered = group.OrderByDescending(p => p.ClockUtc).Take(2).ToList();
            if (ordered.Count < 2)
                continue;

            var newest = ordered[0];
            var previous = ordered[1];
            var dt = (newest.ClockUtc - previous.ClockUtc).TotalSeconds;
            if (dt <= 0)
                continue;

            if (!double.TryParse(newest.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var vNew) ||
                !double.TryParse(previous.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var vOld) ||
                vNew < vOld)
                continue; // contador resetado ou valor não numérico

            rates[group.Key] = (vNew - vOld) / dt;
        }

        return rates;
    }

    /// <summary>ifOperStatus: 1=up; 2(down)/6(notPresent)/7(lowerLayerDown)=down.</summary>
    private static bool? InterpretOperStatus(string? value)
        => value switch
        {
            "1" => true,
            "2" or "6" or "7" => false,
            _ => null,
        };

    /// <summary>dot3StatsDuplexStatus: 2=half, 3=full; 1(unknown) ⇒ null.</summary>
    private static string? InterpretDuplex(string? value)
        => value switch
        {
            "2" => "Half",
            "3" => "Full",
            _ => null,
        };

    /// <summary>
    /// Índice da interface = último segmento do parâmetro da key
    /// (`net.if.status[ifOperStatus.10]` → 10). Null quando não há dígito.
    /// </summary>
    private static int? ExtractIfIndex(string key)
    {
        var open = key.IndexOf('[');
        var close = key.LastIndexOf(']');
        if (open < 0 || close <= open)
            return null;

        var inner = key[(open + 1)..close];
        var dot = inner.LastIndexOf('.');
        var tail = dot >= 0 ? inner[(dot + 1)..] : inner;
        return int.TryParse(tail, out var index) ? index : null;
    }

    /// <summary>
    /// Nome amigável: "Interface GigabitEthernet1/0/10(...)" →
    /// "GigabitEthernet1/0/10"; "Interface port 10: ..." → "port 10".
    /// </summary>
    private static string ExtractDisplayName(string itemName, string key)
    {
        var name = itemName;
        const string prefix = "Interface ";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            name = name[prefix.Length..];

        var cut = name.IndexOfAny(['(', ':']);
        if (cut > 0)
            name = name[..cut];

        name = name.Trim();
        if (name.Length == 0)
        {
            var open = key.IndexOf('[');
            var close = key.LastIndexOf(']');
            name = open >= 0 && close > open ? key[(open + 1)..close] : key;
        }
        return name;
    }
}
