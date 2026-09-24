using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Racks.Commands;

// F2-T002: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (nome repetido no mesmo local).

public sealed record CreateRackCommand(
    Guid LocationId,
    string Name,
    string? Description = null) : BaseCommand<RackDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateRackCommandHandler(
    IRackRepository repository,
    ILocationRepository locationRepository,
    ILogger<CreateRackCommandHandler> logger) : IRequestHandler<CreateRackCommand, RackDto>
{
    public async Task<RackDto> Handle(CreateRackCommand request, CancellationToken cancellationToken)
    {
        var rack = Rack.Create(request.LocationId, request.Name, request.Description);

        var locationExists = await locationRepository.GetByIdAsync(rack.LocationId, cancellationToken);
        if (locationExists is null)
            throw new KeyNotFoundException($"Local '{rack.LocationId}' não encontrado.");

        var existing = await repository.GetByNameAsync(rack.LocationId, rack.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe um rack '{rack.Name}' neste local.");

        await repository.AddAsync(rack, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Rack {RackId} created ({Name}) in location {LocationId}.",
            rack.Id, rack.Name, rack.LocationId);

        return new RackDto(rack.Id, rack.LocationId, rack.Name, rack.Description);
    }
}

public sealed record UpdateRackCommand(
    Guid Id,
    string Name,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateRackCommandHandler(
    IRackRepository repository,
    ILogger<UpdateRackCommandHandler> logger) : IRequestHandler<UpdateRackCommand>
{
    public async Task Handle(UpdateRackCommand request, CancellationToken cancellationToken)
    {
        var rack = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Rack '{request.Id}' não encontrado.");

        rack.Update(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(rack.LocationId, rack.Name, cancellationToken);
        if (existing is not null && existing.Id != rack.Id)
            throw new InvalidOperationException($"Já existe um rack '{rack.Name}' neste local.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rack {RackId} updated ({Name}).", rack.Id, rack.Name);
    }
}

public sealed record DeleteRackCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteRackCommandHandler(
    IRackRepository repository,
    IDeviceRepository deviceRepository,
    ILogger<DeleteRackCommandHandler> logger) : IRequestHandler<DeleteRackCommand>
{
    public async Task Handle(DeleteRackCommand request, CancellationToken cancellationToken)
    {
        var rack = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Rack '{request.Id}' não encontrado.");

        if (await deviceRepository.HasByRackAsync(rack.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir o rack: existem devices vinculados.");

        repository.Remove(rack);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rack {RackId} deleted ({Name}).", rack.Id, rack.Name);
    }
}
