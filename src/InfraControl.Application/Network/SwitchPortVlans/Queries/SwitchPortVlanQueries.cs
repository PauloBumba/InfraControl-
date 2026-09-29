using InfraControl.Application.Network.SwitchPorts;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.Vlans;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.Application.Network.Vlans.Queries;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.SwitchPortVlans.Queries;

// F3-T004: listas dos dois lados da junção. Reusam VlanDto (F3-T003) e
// SwitchPortDto (F3-T002) — sem DTO próprio. Guards reusados dos slices.

public sealed record GetSwitchPortVlansQuery(Guid SwitchPortId)
    : IQuery<IReadOnlyList<VlanDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSwitchPortVlansQueryHandler(
    ISwitchPortVlanRepository switchPortVlanRepository,
    IVlanRepository vlanRepository,
    GetSwitchPortQueryHandler switchPortQueryHandler)
    : IRequestHandler<GetSwitchPortVlansQuery, IReadOnlyList<VlanDto>>
{
    public async Task<IReadOnlyList<VlanDto>> Handle(
        GetSwitchPortVlansQuery request,
        CancellationToken cancellationToken)
    {
        await switchPortQueryHandler.Handle(
            new GetSwitchPortQuery(request.SwitchPortId), cancellationToken);

        var vlanIds = await switchPortVlanRepository.GetVlanIdsByPortAsync(
            request.SwitchPortId, cancellationToken);
        if (vlanIds.Count == 0)
            return [];

        var vlans = await vlanRepository.GetByIdsAsync(vlanIds, cancellationToken);

        return vlans
            .OrderBy(vlan => vlan.VlanNumber)
            .Select(vlan => new VlanDto(vlan.Id, vlan.Name, vlan.VlanNumber, vlan.Description))
            .ToArray();
    }
}

public sealed record GetVlanSwitchPortsQuery(Guid VlanId)
    : IQuery<IReadOnlyList<SwitchPortDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetVlanSwitchPortsQueryHandler(
    ISwitchPortVlanRepository switchPortVlanRepository,
    ISwitchPortRepository switchPortRepository,
    GetVlanQueryHandler vlanQueryHandler)
    : IRequestHandler<GetVlanSwitchPortsQuery, IReadOnlyList<SwitchPortDto>>
{
    public async Task<IReadOnlyList<SwitchPortDto>> Handle(
        GetVlanSwitchPortsQuery request,
        CancellationToken cancellationToken)
    {
        await vlanQueryHandler.Handle(new GetVlanQuery(request.VlanId), cancellationToken);

        var portIds = await switchPortVlanRepository.GetPortIdsByVlanAsync(
            request.VlanId, cancellationToken);
        if (portIds.Count == 0)
            return [];

        var ports = await switchPortRepository.GetByIdsAsync(portIds, cancellationToken);

        return ports
            .OrderBy(port => port.Name)
            .Select(port => new SwitchPortDto(
                port.Id, port.DeviceId, port.Name, port.Description, port.LinkUp))
            .ToArray();
    }
}
