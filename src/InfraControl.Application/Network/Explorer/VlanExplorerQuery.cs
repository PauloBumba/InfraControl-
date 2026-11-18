using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Explorer;

// Network Explorer F2 — VLAN Explorer. VLANs cadastradas (entidade Vlan)
// enriquecidas com: portas/switches associados (SwitchPortVlan → SwitchPort →
// Device), dispositivos descobertos naquela VLAN (leases DHCP distintos por
// MAC) e último avistamento. VLANs vistas em DHCP mas NÃO cadastradas vêm em
// `DiscoveredVlans` — o spec proíbe assumir que toda VLAN do DHCP existe no
// cadastro, e proíbe inventar o que a fonte não informa (subnet/gateway não
// existem no modelo → não aparecem).

public sealed record VlanExplorerSwitchRefDto(Guid DeviceId, string DeviceName, int PortCount);

public sealed record VlanExplorerItemDto(
    Guid? Id,
    int? VlanNumber,
    string? Name,
    string? Description,
    bool IsRegistered,
    int PortCount,
    int DiscoveredDeviceCount,
    DateTime? LastSeenAt,
    IReadOnlyList<VlanExplorerSwitchRefDto> Switches);

public sealed record VlanExplorerResultDto(
    IReadOnlyList<VlanExplorerItemDto> Items,
    IReadOnlyList<VlanExplorerItemDto> DiscoveredVlans);

public sealed record SearchVlansExplorerQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<VlanExplorerResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class SearchVlansExplorerQueryHandler(
    IVlanRepository vlans,
    ISwitchPortVlanRepository portVlans,
    ISwitchPortRepository ports,
    IDeviceRepository devices,
    IDiscoveryResultRepository discoveryResults)
    : IRequestHandler<SearchVlansExplorerQuery, VlanExplorerResultDto>
{
    private const int RegisteredScanSize = 100;
    private const int MaxDiscoveredVlans = 50;

    public async Task<VlanExplorerResultDto> Handle(
        SearchVlansExplorerQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var vlanPage = await vlans.SearchAsync(term, page, pageSize, cancellationToken);

        // DHCP leases de todas as execuções — fonte do "dispositivos descobertos
        // na VLAN" e das VLANs não cadastradas.
        var leases = await discoveryResults.ListByKindsAsync(
            [DiscoveryResultKind.DhcpLease], cancellationToken);
        var discovered = BuildDiscoveredStats(leases);

        // Universo de VLANs registradas (para separar "descoberta mas não
        // cadastrada" das que só estão em outra página do paging).
        var allRegistered = await vlans.SearchAsync(null, 1, RegisteredScanSize, cancellationToken);
        var registeredNumbers = allRegistered.Items.Select(v => v.VlanNumber).ToHashSet();

        var deviceNames = (await devices.GetAllOrderedByNameAsync(cancellationToken))
            .ToDictionary(d => d.Id, d => d.Name);

        var items = new List<VlanExplorerItemDto>(vlanPage.Items.Count);
        foreach (var vlan in vlanPage.Items)
        {
            var switches = await LoadSwitchesAsync(vlan.Id, deviceNames, cancellationToken);
            discovered.TryGetValue(vlan.VlanNumber, out var stats);

            items.Add(new VlanExplorerItemDto(
                vlan.Id,
                vlan.VlanNumber,
                vlan.Name,
                vlan.Description,
                true,
                switches.Sum(s => s.PortCount),
                stats?.DeviceCount ?? 0,
                stats?.LastSeenAt,
                switches));
        }

        var discoveredVlans = discovered
            .Where(kv => !registeredNumbers.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .Take(MaxDiscoveredVlans)
            .Select(kv => new VlanExplorerItemDto(
                null,
                kv.Key,
                null,
                null,
                false,
                0,
                kv.Value.DeviceCount,
                kv.Value.LastSeenAt,
                []))
            .ToArray();

        return new VlanExplorerResultDto(items, discoveredVlans);
    }

    private async Task<IReadOnlyList<VlanExplorerSwitchRefDto>> LoadSwitchesAsync(
        Guid vlanId,
        IReadOnlyDictionary<Guid, string> deviceNames,
        CancellationToken cancellationToken)
    {
        var portIds = await portVlans.GetPortIdsByVlanAsync(vlanId, cancellationToken);
        if (portIds.Count == 0)
            return [];

        var portList = await ports.GetByIdsAsync(portIds, cancellationToken);
        return portList
            .GroupBy(p => p.DeviceId)
            .Select(g => new VlanExplorerSwitchRefDto(
                g.Key,
                deviceNames.TryGetValue(g.Key, out var name) ? name : g.Key.ToString(),
                g.Count()))
            .OrderBy(s => s.DeviceName)
            .ToArray();
    }

    private sealed record VlanStats(int DeviceCount, DateTime? LastSeenAt);

    /// <summary>vlanNumber → (MACs distintos, último avistamento) a partir dos leases DHCP.</summary>
    private static Dictionary<int, VlanStats> BuildDiscoveredStats(
        IReadOnlyList<Domain.Entities.DiscoveryResult> leases)
    {
        var byVlan = new Dictionary<int, HashSet<string>>();
        var lastSeen = new Dictionary<int, DateTime>();

        foreach (var lease in leases)
        {
            if (lease.Values is null
                || !lease.Values.TryGetValue("vlan", out var vlanRaw)
                || !int.TryParse(vlanRaw, out var vlanNumber))
                continue;

            if (!byVlan.TryGetValue(vlanNumber, out var macs))
                byVlan[vlanNumber] = macs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (lease.Values.TryGetValue("mac", out var mac) && !string.IsNullOrWhiteSpace(mac))
                macs.Add(mac.Trim());

            if (!lastSeen.TryGetValue(vlanNumber, out var seen) || lease.CreatedAt > seen)
                lastSeen[vlanNumber] = lease.CreatedAt;
        }

        return byVlan.ToDictionary(
            kv => kv.Key,
            kv => new VlanStats(kv.Value.Count, lastSeen.GetValueOrDefault(kv.Key)));
    }
}
