using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.Vlans;
using InfraControl.Application.Network.Vlans.Queries;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Network.SwitchPortVlans.Commands;

// F3-T004: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409. Os guards de existência são REUSADOS dos
// slices F3-T002 (porta) e F3-T003 (VLAN) via seus QueryHandlers.

public sealed record AssignVlanToSwitchPortCommand(
    Guid SwitchPortId,
    Guid VlanId) : BaseCommand<VlanDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class AssignVlanToSwitchPortCommandHandler(
    ISwitchPortVlanRepository repository,
    GetSwitchPortQueryHandler switchPortQueryHandler,
    GetVlanQueryHandler vlanQueryHandler,
    ILogger<AssignVlanToSwitchPortCommandHandler> logger)
    : IRequestHandler<AssignVlanToSwitchPortCommand, VlanDto>
{
    public async Task<VlanDto> Handle(
        AssignVlanToSwitchPortCommand request,
        CancellationToken cancellationToken)
    {
        // Guards reusados: 404 "Porta '{id}' não encontrado." / "VLAN '{id}' não encontrada."
        await switchPortQueryHandler.Handle(
            new GetSwitchPortQuery(request.SwitchPortId), cancellationToken);
        var vlan = await vlanQueryHandler.Handle(
            new GetVlanQuery(request.VlanId), cancellationToken);

        var existing = await repository.GetPairAsync(
            request.SwitchPortId, request.VlanId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("A porta já está vinculada a esta VLAN.");

        await repository.AddAsync(
            SwitchPortVlan.Create(request.SwitchPortId, request.VlanId),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Vlan {VlanId} assigned to SwitchPort {SwitchPortId}.",
            request.VlanId, request.SwitchPortId);

        return vlan;
    }
}

public sealed record UnassignVlanFromSwitchPortCommand(
    Guid SwitchPortId,
    Guid VlanId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UnassignVlanFromSwitchPortCommandHandler(
    ISwitchPortVlanRepository repository,
    GetSwitchPortQueryHandler switchPortQueryHandler,
    GetVlanQueryHandler vlanQueryHandler,
    ILogger<UnassignVlanFromSwitchPortCommandHandler> logger)
    : IRequestHandler<UnassignVlanFromSwitchPortCommand>
{
    public async Task Handle(
        UnassignVlanFromSwitchPortCommand request,
        CancellationToken cancellationToken)
    {
        await switchPortQueryHandler.Handle(
            new GetSwitchPortQuery(request.SwitchPortId), cancellationToken);
        await vlanQueryHandler.Handle(
            new GetVlanQuery(request.VlanId), cancellationToken);

        var link = await repository.GetPairAsync(
            request.SwitchPortId, request.VlanId, cancellationToken);
        if (link is null)
            throw new KeyNotFoundException(
                $"Vínculo da porta '{request.SwitchPortId}' com a VLAN '{request.VlanId}' não encontrado.");

        repository.Remove(link);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Vlan {VlanId} unassigned from SwitchPort {SwitchPortId}.",
            request.VlanId, request.SwitchPortId);
    }
}
