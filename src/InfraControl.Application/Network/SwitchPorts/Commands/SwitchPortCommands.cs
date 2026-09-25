using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Network.SwitchPorts.Commands;

// F3-T002: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (nome repetido POR SWITCH). O guard de
// existência/type=Switch é REUSADO do F3-T001 via GetSwitchQueryHandler.

public sealed record CreateSwitchPortCommand(
    Guid DeviceId,
    string Name,
    string? Description = null) : BaseCommand<SwitchPortDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateSwitchPortCommandHandler(
    ISwitchPortRepository repository,
    GetSwitchQueryHandler switchQueryHandler,
    ILogger<CreateSwitchPortCommandHandler> logger) : IRequestHandler<CreateSwitchPortCommand, SwitchPortDto>
{
    public async Task<SwitchPortDto> Handle(CreateSwitchPortCommand request, CancellationToken cancellationToken)
    {
        var port = SwitchPort.Create(request.DeviceId, request.Name, request.Description);

        // Reuso do guard F3-T001: device desconhecido ou que não é switch
        // → KeyNotFoundException "Switch '{id}' não encontrado." (404).
        await switchQueryHandler.Handle(new GetSwitchQuery(port.DeviceId), cancellationToken);

        var existing = await repository.GetByNameAsync(port.DeviceId, port.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe uma porta '{port.Name}' neste switch.");

        await repository.AddAsync(port, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SwitchPort {SwitchPortId} created ({Name}) on switch {DeviceId}.",
            port.Id, port.Name, port.DeviceId);

        return new SwitchPortDto(port.Id, port.DeviceId, port.Name, port.Description, port.LinkUp);
    }
}

public sealed record UpdateSwitchPortCommand(
    Guid Id,
    string Name,
    string? Description = null,
    bool? LinkUp = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateSwitchPortCommandHandler(
    ISwitchPortRepository repository,
    ILogger<UpdateSwitchPortCommandHandler> logger) : IRequestHandler<UpdateSwitchPortCommand>
{
    public async Task Handle(UpdateSwitchPortCommand request, CancellationToken cancellationToken)
    {
        var port = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Porta '{request.Id}' não encontrado.");

        port.Update(request.Name, request.Description, request.LinkUp);

        var existing = await repository.GetByNameAsync(port.DeviceId, port.Name, cancellationToken);
        if (existing is not null && existing.Id != port.Id)
            throw new InvalidOperationException($"Já existe uma porta '{port.Name}' neste switch.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SwitchPort {SwitchPortId} updated ({Name}, LinkUp={LinkUp}).",
            port.Id, port.Name, port.LinkUp);
    }
}

public sealed record DeleteSwitchPortCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteSwitchPortCommandHandler(
    ISwitchPortRepository repository,
    ISwitchPortVlanRepository switchPortVlanRepository,
    ILogger<DeleteSwitchPortCommandHandler> logger) : IRequestHandler<DeleteSwitchPortCommand>
{
    public async Task Handle(DeleteSwitchPortCommand request, CancellationToken cancellationToken)
    {
        var port = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Porta '{request.Id}' não encontrado.");

        // F3-T004: portas com VLANs vinculadas não podem ser excluídas.
        if (await switchPortVlanRepository.HasVlanLinksAsync(port.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir a porta: existem VLANs vinculadas.");

        repository.Remove(port);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SwitchPort {SwitchPortId} deleted ({Name}).", port.Id, port.Name);
    }
}
