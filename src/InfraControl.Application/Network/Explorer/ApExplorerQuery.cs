using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Explorer;

// Network Explorer F3 — AP/AC Explorer. Inventário central (§1/§6):
// - APs = Devices type AccessPoint + devices UniFi tipo AP sem cadastro
//   (IsRegistered=false — visível, nunca escondido).
// - ACs = devices UniFi tipo gateway/controller (UDM/UXG/UCN…), casados por
//   MAC com o inventário quando houver.
// Enriquecimento sempre OPCIONAL e ao vivo (REGRA 8 — nada de telemetria
// inventada): UniFi conectado/clientes/SSIDs; uplink via LLDP (Neighbor)
// quando o discovery já viu o AP a partir de um switch; VLAN de gerência e
// localização do cadastro; último discovery run por device.

public sealed record ApExplorerClientDto(
    string Mac,
    string? Ip,
    string? Hostname,
    string? Essid,
    int? Signal);

public sealed record ApExplorerItemDto(
    Guid? DeviceId,
    string Name,
    /// <summary>"AccessPoint" | "Controller".</summary>
    string Kind,
    bool IsRegistered,
    string? Ip,
    string? Mac,
    string? LocationName,
    string? ManagementVlan,
    string? UplinkSwitchName,
    string? UplinkPort,
    bool? UniFiConnected,
    int ClientCount,
    IReadOnlyList<string> Ssids,
    IReadOnlyList<ApExplorerClientDto> Clients,
    DateTime? LastDiscoveryAt,
    string? LastRunStatus);

public sealed record ApExplorerResultDto(IReadOnlyList<ApExplorerItemDto> Items);

public sealed record SearchApExplorerQuery()
    : IQuery<ApExplorerResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class SearchApExplorerQueryHandler(
    IDeviceRepository devices,
    ILocationRepository locations,
    IVlanRepository vlans,
    IDiscoveryResultRepository discoveryResults,
    IDiscoveryRunRepository discoveryRuns,
    IUnifiApiClient unifi)
    : IRequestHandler<SearchApExplorerQuery, ApExplorerResultDto>
{
    private const int MaxClientsPerAp = 50;
    private const int RunScanSize = 100;

    public async Task<ApExplorerResultDto> Handle(
        SearchApExplorerQuery request,
        CancellationToken cancellationToken)
    {
        var allDevices = await devices.GetAllOrderedByNameAsync(cancellationToken);
        var deviceByMac = allDevices
            .Where(d => !string.IsNullOrWhiteSpace(d.Mac))
            .GroupBy(d => MacNormalizer.Normalize(d.Mac)!)
            .ToDictionary(g => g.Key, g => g.First());

        var registeredAps = allDevices
            .Where(d => d.Type == DeviceType.AccessPoint)
            .ToArray();

        // Fontes opcionais — cada falha isola só o enriquecimento dela.
        var unifiDevices = await TryAsync(() => unifi.ListDevicesAsync(cancellationToken), []);
        var unifiAps = await TryAsync(() => unifi.ListAccessPointsAsync(cancellationToken), []);
        var unifiClients = await TryAsync(() => unifi.ListConnectedClientsAsync(cancellationToken), []);

        var neighbors = await discoveryResults.ListByKindsAsync(
            [DiscoveryResultKind.Neighbor], cancellationToken);
        var runs = await discoveryRuns.SearchAsync(null, 1, RunScanSize, cancellationToken);
        var lastRunByDevice = runs.Items
            .GroupBy(r => r.DeviceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.StartedAt).First());

        var locationNames = new Dictionary<Guid, string>();
        foreach (var id in registeredAps.Select(d => d.LocationId).Distinct())
        {
            var location = await locations.GetByIdAsync(id, cancellationToken);
            if (location is not null)
                locationNames[id] = location.Name;
        }

        var vlanNames = new Dictionary<Guid, string>();
        foreach (var id in registeredAps.Where(d => d.VlanId is not null).Select(d => d.VlanId!.Value).Distinct())
        {
            var vlan = await vlans.GetByIdAsync(id, cancellationToken);
            if (vlan is not null)
                vlanNames[id] = vlan.Name;
        }

        var registeredMacs = registeredAps
            .Select(d => MacNormalizer.Normalize(d.Mac))
            .Where(m => m is not null)
            .ToHashSet()!;

        var items = new List<ApExplorerItemDto>();

        // 1) APs do inventário — sempre listados.
        foreach (var ap in registeredAps)
        {
            items.Add(await BuildItemAsync(
                ap, "AccessPoint", true, ap.Mac,
                unifiAps, unifiClients, neighbors, allDevices,
                lastRunByDevice, locationNames, vlanNames, cancellationToken));
        }

        // 2) APs vistos no UniFi sem cadastro (não duplicar os já registrados).
        foreach (var unifiAp in unifiAps)
        {
            var mac = MacNormalizer.Normalize(unifiAp.Mac);
            if (mac is not null && registeredMacs.Contains(mac))
                continue;

            var matched = mac is not null && deviceByMac.TryGetValue(mac, out var d) ? d : null;
            items.Add(BuildUnregisteredItem(
                unifiAp.Name, "AccessPoint", unifiAp.Mac, unifiAp.Ip,
                unifiAp.Connected, unifiAp.Clients,
                unifiClients.Where(c => MacNormalizer.Normalize(c.ApMac) == mac).ToArray(),
                neighbors, allDevices, matched));
        }

        // 3) Controladores/ACs UniFi (gateway/controller) — casados por MAC.
        foreach (var unifiDevice in unifiDevices.Where(IsControllerType))
        {
            var mac = MacNormalizer.Normalize(unifiDevice.Mac);
            var matched = mac is not null && deviceByMac.TryGetValue(mac, out var d) ? d : null;
            // Device UniFi controller que já apareceu como AP não vira AC duplicado.
            if (matched is not null && matched.Type == DeviceType.AccessPoint)
                continue;

            if (matched is not null)
            {
                items.Add(await BuildItemAsync(
                    matched, "Controller", true, matched.Mac,
                    unifiAps, unifiClients, neighbors, allDevices,
                    lastRunByDevice, locationNames, vlanNames, cancellationToken));
            }
            else
            {
                items.Add(new ApExplorerItemDto(
                    null, unifiDevice.Name, "Controller", false,
                    unifiDevice.Ip, unifiDevice.Mac,
                    null, null, null, null, null, 0, [], [],
                    null, null));
            }
        }

        return new ApExplorerResultDto(
            items.OrderBy(i => i.IsRegistered ? 0 : 1)
                 .ThenBy(i => i.Kind)
                 .ThenBy(i => i.Name)
                 .ToArray());
    }

    private async Task<ApExplorerItemDto> BuildItemAsync(
        Device device,
        string kind,
        bool isRegistered,
        string? mac,
        IReadOnlyList<UnifiAccessPointDto> unifiAps,
        IReadOnlyList<UnifiConnectedClientDto> unifiClients,
        IReadOnlyList<DiscoveryResult> neighbors,
        IReadOnlyList<Device> allDevices,
        IReadOnlyDictionary<Guid, DiscoveryRun> lastRunByDevice,
        IReadOnlyDictionary<Guid, string> locationNames,
        IReadOnlyDictionary<Guid, string> vlanNames,
        CancellationToken cancellationToken)
    {
        var normalizedMac = MacNormalizer.Normalize(mac ?? device.Mac);
        var unifiAp = normalizedMac is null
            ? null
            : unifiAps.FirstOrDefault(a => MacNormalizer.Normalize(a.Mac) == normalizedMac);
        IReadOnlyList<ApExplorerClientDto> clients = normalizedMac is null
            ? []
            : unifiClients
                .Where(c => MacNormalizer.Normalize(c.ApMac) == normalizedMac && !c.IsWired)
                .Take(MaxClientsPerAp)
                .Select(c => new ApExplorerClientDto(
                    c.Mac, c.Ip, c.Hostname, c.Essid, c.Signal))
                .ToArray();

        var (uplinkSwitch, uplinkPort) = ResolveUplink(neighbors, allDevices, device.Name, normalizedMac);
        lastRunByDevice.TryGetValue(device.Id, out var lastRun);

        return new ApExplorerItemDto(
            device.Id,
            device.Name,
            kind,
            isRegistered,
            device.Ip ?? unifiAp?.Ip,
            device.Mac ?? unifiAp?.Mac,
            locationNames.GetValueOrDefault(device.LocationId),
            device.VlanId is { } vlanId ? vlanNames.GetValueOrDefault(vlanId) : null,
            uplinkSwitch,
            uplinkPort,
            unifiAp?.Connected,
            unifiAp?.Clients ?? clients.Count,
            clients.Select(c => c.Essid).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToArray(),
            clients,
            lastRun?.StartedAt,
            lastRun?.Status.ToString());
    }

    private ApExplorerItemDto BuildUnregisteredItem(
        string name,
        string kind,
        string mac,
        string? ip,
        bool connected,
        int unifiClientCount,
        IReadOnlyList<UnifiConnectedClientDto> clients,
        IReadOnlyList<DiscoveryResult> neighbors,
        IReadOnlyList<Device> allDevices,
        Device? matchedDevice)
    {
        var normalizedMac = MacNormalizer.Normalize(mac);
        var clientDtos = clients
            .Take(MaxClientsPerAp)
            .Select(c => new ApExplorerClientDto(c.Mac, c.Ip, c.Hostname, c.Essid, c.Signal))
            .ToArray();
        var (uplinkSwitch, uplinkPort) = ResolveUplink(neighbors, allDevices, name, normalizedMac);

        return new ApExplorerItemDto(
            matchedDevice?.Id,
            name,
            kind,
            matchedDevice is not null,
            ip,
            mac,
            null,
            null,
            uplinkSwitch,
            uplinkPort,
            connected,
            unifiClientCount > 0 ? unifiClientCount : clientDtos.Length,
            clientDtos.Select(c => c.Essid).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToArray(),
            clientDtos,
            null,
            null);
    }

    /// <summary>
    /// Uplink via LLDP: Neighbor rows cujo chassis/sys_name casam com o AP —
    /// o DeviceId do resultado é o switch que está vendo o AP; local_port é a
    /// porta desse switch. Mais recente vence; null = sem evidência.
    /// </summary>
    private static (string? Switch, string? Port) ResolveUplink(
        IReadOnlyList<DiscoveryResult> neighbors,
        IReadOnlyList<Device> allDevices,
        string apName,
        string? apMac)
    {
        var deviceNames = allDevices.ToDictionary(d => d.Id, d => d.Name);

        var match = neighbors
            .Where(r => r.Values is not null)
            .Where(r =>
            {
                var chassis = Value(r, "chassis_id");
                var sysName = Value(r, "sys_name");
                return (apMac is not null && MacNormalizer.Normalize(chassis) == apMac)
                    || (!string.IsNullOrWhiteSpace(sysName)
                        && sysName.Equals(apName, StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        if (match is null)
            return (null, null);

        return (
            match.DeviceId is { } id && deviceNames.TryGetValue(id, out var n) ? n : null,
            FirstNonEmpty(Value(match, "local_port"), Value(match, "port_id"), Value(match, "local_if_name")));
    }

    private static bool IsControllerType(UnifiDeviceDto device) =>
        device.Type.Contains("gw", StringComparison.OrdinalIgnoreCase)
        || device.Type.Contains("uxg", StringComparison.OrdinalIgnoreCase)
        || device.Type.Contains("udm", StringComparison.OrdinalIgnoreCase)
        || device.Type.Contains("ucg", StringComparison.OrdinalIgnoreCase)
        || device.Type.Equals("ac", StringComparison.OrdinalIgnoreCase);

    private static async Task<IReadOnlyList<T>> TryAsync<T>(
        Func<Task<IReadOnlyList<T>>> call,
        IReadOnlyList<T> fallback)
    {
        try { return await call() ?? fallback; }
        catch { return fallback; }
    }

    private static string? Value(DiscoveryResult row, string key) =>
        row.Values is not null && row.Values.TryGetValue(key, out var value) ? value : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
