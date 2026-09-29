using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.Locations.Commands;

// F2-T001: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler (F0-T023): ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409.

public sealed record CreateLocationCommand(
    string Name,
    string? Description = null) : BaseCommand<LocationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateLocationCommandHandler(
    ILocationRepository repository,
    ILogger<CreateLocationCommandHandler> logger) : IRequestHandler<CreateLocationCommand, LocationDto>
{
    public async Task<LocationDto> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = Location.Create(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(location.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("Já existe um local com este nome.");

        await repository.AddAsync(location, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Location {LocationId} created ({Name}).", location.Id, location.Name);

        return new LocationDto(location.Id, location.Name, location.Description);
    }
}

public sealed record UpdateLocationCommand(
    Guid Id,
    string Name,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateLocationCommandHandler(
    ILocationRepository repository,
    ILogger<UpdateLocationCommandHandler> logger) : IRequestHandler<UpdateLocationCommand>
{
    public async Task Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Local '{request.Id}' não encontrado.");

        location.Update(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(location.Name, cancellationToken);
        if (existing is not null && existing.Id != location.Id)
            throw new InvalidOperationException("Já existe um local com este nome.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Location {LocationId} updated ({Name}).", location.Id, location.Name);
    }
}

public sealed record DeleteLocationCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteLocationCommandHandler(
    ILocationRepository repository,
    ILogger<DeleteLocationCommandHandler> logger) : IRequestHandler<DeleteLocationCommand>
{
    public async Task Handle(DeleteLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Local '{request.Id}' não encontrado.");

        if (await repository.HasRacksAsync(location.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir o local: existem racks vinculados.");

        repository.Remove(location);
        await repository.SaveChangesAsync(cancellationToken);

        // F2-T002: exclusão bloqueada acima quando há racks; a FK Restrict no
        // banco é o backstop. Devices (F2-T003) passam a bloquear exclusões de rack.
        logger.LogInformation("Location {LocationId} deleted ({Name}).", location.Id, location.Name);
    }
}
