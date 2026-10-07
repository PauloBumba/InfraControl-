using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.MacAddresses.Commands;

// F2-T005: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (MAC duplicado — unicidade global).

public sealed record CreateMacAddressCommand(
    Guid InterfaceId,
    string Mac) : BaseCommand<MacAddressDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateMacAddressCommandHandler(
    IMacAddressRepository repository,
    INetworkInterfaceRepository interfaceRepository,
    ILogger<CreateMacAddressCommandHandler> logger) : IRequestHandler<CreateMacAddressCommand, MacAddressDto>
{
    public async Task<MacAddressDto> Handle(CreateMacAddressCommand request, CancellationToken cancellationToken)
    {
        var mac = MacAddress.Create(request.InterfaceId, request.Mac);

        var nicExists = await interfaceRepository.GetByIdAsync(mac.InterfaceId, cancellationToken);
        if (nicExists is null)
            throw new KeyNotFoundException($"Interface '{mac.InterfaceId}' não encontrado.");

        var existing = await repository.GetByMacAsync(mac.Mac, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe um cadastro com o MAC '{mac.Mac}'.");

        await repository.AddAsync(mac, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "MacAddress {MacAddressId} ({Mac}) created on interface {InterfaceId}.",
            mac.Id, mac.Mac, mac.InterfaceId);

        return new MacAddressDto(mac.Id, mac.InterfaceId, mac.Mac);
    }
}

public sealed record UpdateMacAddressCommand(
    Guid Id,
    string Mac) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateMacAddressCommandHandler(
    IMacAddressRepository repository,
    ILogger<UpdateMacAddressCommandHandler> logger) : IRequestHandler<UpdateMacAddressCommand>
{
    public async Task Handle(UpdateMacAddressCommand request, CancellationToken cancellationToken)
    {
        var mac = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"MAC '{request.Id}' não encontrado.");

        mac.Update(request.Mac);

        var existing = await repository.GetByMacAsync(mac.Mac, cancellationToken);
        if (existing is not null && existing.Id != mac.Id)
            throw new InvalidOperationException($"Já existe um cadastro com o MAC '{mac.Mac}'.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("MacAddress {MacAddressId} updated ({Mac}).", mac.Id, mac.Mac);
    }
}

public sealed record DeleteMacAddressCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteMacAddressCommandHandler(
    IMacAddressRepository repository,
    ILogger<DeleteMacAddressCommandHandler> logger) : IRequestHandler<DeleteMacAddressCommand>
{
    public async Task Handle(DeleteMacAddressCommand request, CancellationToken cancellationToken)
    {
        var mac = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"MAC '{request.Id}' não encontrado.");

        mac.Delete(); // ADR-030 §5 — macDeletedDomainEvent na mesma transação
        repository.Remove(mac);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("MacAddress {MacAddressId} deleted ({Mac}).", mac.Id, mac.Mac);
    }
}
