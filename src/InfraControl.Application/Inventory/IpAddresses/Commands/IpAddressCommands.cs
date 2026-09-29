using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.IpAddresses.Commands;

// F2-T006: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (IP duplicado — unicidade global).

public sealed record CreateIpAddressCommand(
    Guid InterfaceId,
    string Ip) : BaseCommand<IpAddressDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateIpAddressCommandHandler(
    IIpAddressRepository repository,
    INetworkInterfaceRepository interfaceRepository,
    ILogger<CreateIpAddressCommandHandler> logger) : IRequestHandler<CreateIpAddressCommand, IpAddressDto>
{
    public async Task<IpAddressDto> Handle(CreateIpAddressCommand request, CancellationToken cancellationToken)
    {
        var entity = IpAddress.Create(request.InterfaceId, request.Ip);

        var nicExists = await interfaceRepository.GetByIdAsync(entity.InterfaceId, cancellationToken);
        if (nicExists is null)
            throw new KeyNotFoundException($"Interface '{entity.InterfaceId}' não encontrado.");

        var existing = await repository.GetByIpAsync(entity.Ip, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe um cadastro com o IP '{entity.Ip}'.");

        await repository.AddAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "IpAddress {IpAddressId} ({Ip}) created on interface {InterfaceId}.",
            entity.Id, entity.Ip, entity.InterfaceId);

        return new IpAddressDto(entity.Id, entity.InterfaceId, entity.Ip);
    }
}

public sealed record UpdateIpAddressCommand(
    Guid Id,
    string Ip) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateIpAddressCommandHandler(
    IIpAddressRepository repository,
    ILogger<UpdateIpAddressCommandHandler> logger) : IRequestHandler<UpdateIpAddressCommand>
{
    public async Task Handle(UpdateIpAddressCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"IP '{request.Id}' não encontrado.");

        entity.Update(request.Ip);

        var existing = await repository.GetByIpAsync(entity.Ip, cancellationToken);
        if (existing is not null && existing.Id != entity.Id)
            throw new InvalidOperationException($"Já existe um cadastro com o IP '{entity.Ip}'.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("IpAddress {IpAddressId} updated ({Ip}).", entity.Id, entity.Ip);
    }
}

public sealed record DeleteIpAddressCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteIpAddressCommandHandler(
    IIpAddressRepository repository,
    ILogger<DeleteIpAddressCommandHandler> logger) : IRequestHandler<DeleteIpAddressCommand>
{
    public async Task Handle(DeleteIpAddressCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"IP '{request.Id}' não encontrado.");

        repository.Remove(entity);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("IpAddress {IpAddressId} deleted ({Ip}).", entity.Id, entity.Ip);
    }
}
