using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.SwitchPorts;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Explorer;

// Network Explorer F2 — Switch detail. Reusa a guarda GetSwitchQueryHandler
// (device tipo Switch, 404 caso contrário) e as portas/VLANs já cadastradas;
// enriquece com os MACs aprendidos via SNMP (MacTableEntry do PRÓPRIO switch)
// cruzados com ARP/DHCP para IP/hostname/VLAN — tudo dado real coletado,
// campo null = fonte não informou.

public sealed record SwitchExplorerVlanRefDto(Guid Id, int Number, string Name);

public sealed record SwitchExplorerPortDto(
    Guid Id,
    string Name,
    string? Description,
    bool? LinkUp,
    string? Speed,
    string? Duplex,
    IReadOnlyList<SwitchExplorerVlanRefDto> Vlans);

public sealed record SwitchExplorerMacDto(
    string Mac,
    string? Ip,
    string? Hostname,
    string? Vlan,
    string? BridgePort,
    string? PortName,
    DateTime SeenAt,
    int AgeDays);

public sealed record SwitchExplorerDto(
    Guid DeviceId,
    string Name,
    string? Ip,
    string? Mac,
    string Type,
    IReadOnlyList<SwitchExplorerPortDto> Ports,
    IReadOnlyList<SwitchExplorerMacDto> LearnedMacs);

public sealed record GetSwitchExplorerQuery(Guid DeviceId)
    : IQuery<SwitchExplorerDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetSwitchExplorerQueryHandler(
    GetSwitchQueryHandler switchQuery,
    ISwitchPortRepository ports,
    ISwitchPortVlanRepository portVlans,
    IVlanRepository vlans,
    IDiscoveryResultRepository discoveryResults)
    : IRequestHandler<GetSwitchExplorerQuery, SwitchExplorerDto>
{
    private const int MaxPorts = 100;
    private const int MaxLearnedMacs = 200;

    public async Task<SwitchExplorerDto> Handle(
        GetSwitchExplorerQuery request,
        CancellationToken cancellationToken)
    {
        var device = await switchQuery.Handle(new GetSwitchQuery(request.DeviceId), cancellationToken);

        var portPage = await ports.SearchAsync(
            request.DeviceId, null, 1, MaxPorts, cancellationToken);
        var portList = portPage.Items;

        var links = await portVlans.GetByPortIdsAsync(
            portList.Select(p => p.Id), cancellationToken);
        var vlanIds = links.Select(l => l.VlanId).Distinct().ToArray();
        var vlanById = (await vlans.GetByIdsAsync(vlanIds, cancellationToken))
            .ToDictionary(v => v.Id);
        var vlansByPort = links
            .Where(l => vlanById.ContainsKey(l.VlanId))
            .GroupBy(l => l.SwitchPortId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SwitchExplorerVlanRefDto>)g
                    .Select(l => vlanById[l.VlanId])
                    .OrderBy(v => v.VlanNumber)
                    .Select(v => new SwitchExplorerVlanRefDto(v.Id, v.VlanNumber, v.Name))
                    .ToArray());

        var portDtos = portList.Select(port => new SwitchExplorerPortDto(
            port.Id,
            port.Name,
            port.Description,
            port.LinkUp,
            port.Speed?.ToString(),
            port.Duplex?.ToString(),
            vlansByPort.TryGetValue(port.Id, out var v) ? v : [])).ToArray();

        var learnedMacs = await LoadLearnedMacsAsync(request.DeviceId, portList, cancellationToken);

        return new SwitchExplorerDto(
            device.Id,
            device.Name,
            device.Ip,
            device.Mac,
            device.Type,
            portDtos,
            learnedMacs);
    }

    /// <summary>
    /// MACs aprendidos por ESTE switch (MacTableEntry com DeviceId do switch),
    /// deduplicados por MAC normalizado (fica a ocorrência mais recente) e
    /// enriquecidos com IP/hostname/VLAN do ARP/DHCP mais recente do mesmo MAC.
    /// </summary>
    private async Task<IReadOnlyList<SwitchExplorerMacDto>> LoadLearnedMacsAsync(
        Guid deviceId,
        IReadOnlyCollection<SwitchPort> portList,
        CancellationToken cancellationToken)
    {
        var rows = await discoveryResults.ListByKindsAsync(
            [DiscoveryResultKind.MacTableEntry, DiscoveryResultKind.ArpEntry, DiscoveryResultKind.DhcpLease],
            cancellationToken);

        var macEntries = rows
            .Where(r => r.Kind == DiscoveryResultKind.MacTableEntry && r.DeviceId == deviceId)
            .Select(r => new { Row = r, Mac = Value(r, "mac"), Port = Value(r, "bridge_port") })
            .Where(x => MacNormalizer.Normalize(x.Mac) is not null)
            .GroupBy(x => MacNormalizer.Normalize(x.Mac)!)
            .Select(g => g.OrderByDescending(x => x.Row.CreatedAt).First())
            .OrderByDescending(x => x.Row.CreatedAt)
            .Take(MaxLearnedMacs)
            .ToArray();

        if (macEntries.Length == 0)
            return [];

        var l3ByMac = rows
            .Where(r => r.Kind is DiscoveryResultKind.ArpEntry or DiscoveryResultKind.DhcpLease)
            .Select(r => new
            {
                Row = r,
                Mac = MacNormalizer.Normalize(Value(r, "mac")),
                Ip = Value(r, "ip"),
                Hostname = Value(r, "hostname"),
                Vlan = Value(r, "vlan"),
            })
            .Where(x => x.Mac is not null)
            .GroupBy(x => x.Mac!)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Row.CreatedAt)
                    .ThenByDescending(x => x.Row.Kind == DiscoveryResultKind.DhcpLease ? 1 : 0)
                    .First());

        var portByName = portList.ToDictionary(p => p.Name, p => p.Name, StringComparer.OrdinalIgnoreCase);

        return macEntries.Select(entry =>
        {
            var normalized = MacNormalizer.Normalize(entry.Mac)!;
            l3ByMac.TryGetValue(normalized, out var l3);
            var portName = entry.Port is { } bridge
                && portByName.TryGetValue(bridge, out var matched)
                ? matched
                : entry.Port;

            return new SwitchExplorerMacDto(
                entry.Mac!,
                l3?.Ip,
                l3?.Hostname,
                l3?.Vlan,
                entry.Port,
                portName,
                entry.Row.CreatedAt,
                Math.Max(0, (int)(DateTime.UtcNow - entry.Row.CreatedAt).TotalDays));
        }).ToArray();
    }

    private static string? Value(DiscoveryResult row, string key) =>
        row.Values is not null && row.Values.TryGetValue(key, out var value) ? value : null;
}
