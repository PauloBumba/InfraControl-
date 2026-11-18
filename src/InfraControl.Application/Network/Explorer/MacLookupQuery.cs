using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Explorer;

// Network Explorer F1 — MAC/IP Explorer. Responde "onde esse MAC/IP está"
// cruzando as evidências reais já coletadas pelos providers de discovery:
// MacTableEntry (SNMP dot1dTpFdbTable → switch + porta), ArpEntry,
// DhcpLease (hostname/VLAN) e Neighbor (LLDP). Values é JSON texto — o
// cruzamento é em memória, mesmo padrão documentado do SearchClientsQuery.
//
// Regras do spec: nunca inventar dado (campo null = fonte não informou);
// registro antigo é sinalizado (IsStale), não escondido; ausência de
// evidência não significa "offline" — a UI explicita isso.

/// <summary>Pesquisa única do MAC Explorer (term = MAC, IP, hostname ou VLAN).</summary>
public sealed record SearchMacLookupQuery(string Term)
    : IQuery<MacLookupResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchMacLookupQueryHandler(
    IDiscoveryResultRepository discoveryResults,
    IDeviceRepository devices,
    IUnifiApiClient unifi)
    : IRequestHandler<SearchMacLookupQuery, MacLookupResultDto>
{
    /// <summary>Janela de "registro antigo": evidência mais velha que isso sinaliza IsStale.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    private const int MaxSightings = 100;

    private static readonly DiscoveryResultKind[] LookupKinds =
    [
        DiscoveryResultKind.MacTableEntry,
        DiscoveryResultKind.ArpEntry,
        DiscoveryResultKind.DhcpLease,
        DiscoveryResultKind.Neighbor,
    ];

    public async Task<MacLookupResultDto> Handle(
        SearchMacLookupQuery request,
        CancellationToken cancellationToken)
    {
        var term = request.Term?.Trim() ?? string.Empty;
        if (term.Length < 2)
        {
            return new MacLookupResultDto(
                term, null, null, null, null, false, null, null, null, false, null, []);
        }

        var rows = await discoveryResults.ListByKindsAsync(LookupKinds, cancellationToken);
        var matched = rows.Where(row => Matches(row, term)).ToList();

        var deviceNames = await LoadDeviceNamesAsync(matched, cancellationToken);
        var sightings = matched
            .Select(row => ToSighting(row, deviceNames))
            .OrderBy(sighting => KindPriority(sighting.Kind))
            .ThenByDescending(sighting => sighting.SeenAt)
            .Take(MaxSightings)
            .ToArray();

        var (wifiClient, apName, ssid, signal) = await ResolveWifiAsync(sightings, cancellationToken);

        return new MacLookupResultDto(
            term,
            FirstNonEmpty(sightings.Select(s => s.Mac).ToArray()),
            FirstPreferred(sightings, s => s.Ip),
            FirstPreferred(sightings, s => s.Hostname),
            FirstPreferred(sightings, s => s.Vlan),
            wifiClient,
            apName,
            ssid,
            signal,
            sightings.Length > 0 && sightings.Max(s => s.SeenAt) < DateTime.UtcNow - StaleAfter,
            sightings.Length > 0 ? sightings.Max(s => s.SeenAt) : null,
            sightings);
    }

    // ── Matching ──────────────────────────────────────────────────────────

    private static bool Matches(DiscoveryResult row, string term)
    {
        if (row.Values is null)
            return false;

        var normalizedTerm = MacNormalizer.Normalize(term);
        var isMacTerm = normalizedTerm is { Length: 12 };

        if (isMacTerm)
        {
            // Termo MAC completo: casa só por MAC normalizado — IP/hostname
            // não devem trazer a tabela MAC inteira por coincidência.
            return MacNormalizer.Normalize(Value(row, "mac")) == normalizedTerm;
        }

        return Contains(Value(row, "mac"), term)
            || Contains(Value(row, "ip"), term)
            || Contains(Value(row, "hostname"), term)
            || Contains(Value(row, "vlan"), term);
    }

    private static string? Value(DiscoveryResult row, string key) =>
        row.Values is not null && row.Values.TryGetValue(key, out var value)
            ? value
            : null;

    private static bool Contains(string? field, string term) =>
        field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    // ── Projeção ──────────────────────────────────────────────────────────

    private static int KindPriority(string kind) => kind switch
    {
        nameof(DiscoveryResultKind.MacTableEntry) => 0,
        nameof(DiscoveryResultKind.DhcpLease) => 1,
        nameof(DiscoveryResultKind.ArpEntry) => 2,
        _ => 3,
    };

    private static MacLookupSightingDto ToSighting(
        DiscoveryResult row,
        IReadOnlyDictionary<Guid, Device> deviceNames)
    {
        var values = row.Values ?? new Dictionary<string, string>();
        string? Get(string key) => values.TryGetValue(key, out var v) ? v : null;

        var device = row.DeviceId is { } id && deviceNames.TryGetValue(id, out var d) ? d : null;

        return new MacLookupSightingDto(
            row.Kind.ToString(),
            KindLabel(row.Kind),
            row.DeviceId,
            device?.Name,
            device?.Ip,
            FirstNonEmpty(Get("bridge_port"), Get("port_id"), Get("local_if")),
            Get("port_desc"),
            Get("mac"),
            Get("ip"),
            Get("hostname"),
            Get("vlan"),
            row.CreatedAt,
            Math.Max(0, (int)(DateTime.UtcNow - row.CreatedAt).TotalDays));
    }

    private static string KindLabel(DiscoveryResultKind kind) => kind switch
    {
        DiscoveryResultKind.MacTableEntry => "Tabela MAC (SNMP)",
        DiscoveryResultKind.ArpEntry => "ARP",
        DiscoveryResultKind.DhcpLease => "DHCP",
        DiscoveryResultKind.Neighbor => "LLDP",
        _ => kind.ToString(),
    };

    private async Task<IReadOnlyDictionary<Guid, Device>> LoadDeviceNamesAsync(
        IReadOnlyCollection<DiscoveryResult> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Where(r => r.DeviceId is not null)
            .Select(r => r.DeviceId!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, Device>();

        var all = await devices.GetAllOrderedByNameAsync(cancellationToken);
        return all.Where(d => ids.Contains(d.Id)).ToDictionary(d => d.Id);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    /// <summary>Prefere DHCP/ARP para IP/hostname/VLAN; cai na primeira evidência disponível.</summary>
    private static string? FirstPreferred(
        IReadOnlyList<MacLookupSightingDto> sightings,
        Func<MacLookupSightingDto, string?> pick)
    {
        var preferred = sightings
            .Where(s => s.Kind is nameof(DiscoveryResultKind.DhcpLease) or nameof(DiscoveryResultKind.ArpEntry))
            .Select(pick)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return preferred ?? sightings.Select(pick).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    // ── UniFi (AP do cliente Wi-Fi — opcional, nunca inventa) ─────────────

    private async Task<(bool Wifi, string? ApName, string? Ssid, int? Signal)> ResolveWifiAsync(
        IReadOnlyList<MacLookupSightingDto> sightings,
        CancellationToken cancellationToken)
    {
        var mac = sightings.Select(s => s.Mac).FirstOrDefault(m => MacNormalizer.Normalize(m) is not null);
        if (mac is null)
            return (false, null, null, null);

        IReadOnlyList<UnifiConnectedClientDto> clients;
        try
        {
            clients = await unifi.ListConnectedClientsAsync(cancellationToken);
        }
        catch
        {
            return (false, null, null, null); // integração indisponível — segue sem AP
        }

        var normalized = MacNormalizer.Normalize(mac)!;
        var client = (clients ?? []).FirstOrDefault(c => MacNormalizer.Normalize(c.Mac) == normalized);
        if (client is null)
            return (false, null, null, null);

        string? apName = null;
        if (!string.IsNullOrWhiteSpace(client.ApMac))
        {
            var ap = await devices.GetByMacAsync(client.ApMac, cancellationToken);
            apName = ap?.Name;
        }

        return (!client.IsWired, apName, client.Essid ?? client.NetworkName, client.Signal);
    }
}
