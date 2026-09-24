using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.NetworkInterfaces.Commands;

// F2-T004: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (nome repetido por device).

public sealed record CreateNetworkInterfaceCommand(
    Guid DeviceId,
    string Name,
    string? Description = null) : BaseCommand<NetworkInterfaceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateNetworkInterfaceCommandHandler(
    INetworkInterfaceRepository repository,
    IDeviceRepository deviceRepository,
    ILogger<CreateNetworkInterfaceCommandHandler> logger) : IRequestHandler<CreateNetworkInterfaceCommand, NetworkInterfaceDto>
{
    public async Task<NetworkInterfaceDto> Handle(CreateNetworkInterfaceCommand request, CancellationToken cancellationToken)
    {
        var nic = NetworkInterface.Create(request.DeviceId, request.Name, request.Description);

        var deviceExists = await deviceRepository.GetByIdAsync(nic.DeviceId, cancellationToken);
        if (deviceExists is null)
            throw new KeyNotFoundException($"Device '{nic.DeviceId}' não encontrado.");

        var existing = await repository.GetByNameAsync(nic.DeviceId, nic.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe uma interface '{nic.Name}' neste device.");

        await repository.AddAsync(nic, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "NetworkInterface {InterfaceId} created ({Name}) on device {DeviceId}.",
            nic.Id, nic.Name, nic.DeviceId);

        return new NetworkInterfaceDto(nic.Id, nic.DeviceId, nic.Name, nic.Description);
    }
}

public sealed record UpdateNetworkInterfaceCommand(
    Guid Id,
    string Name,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateNetworkInterfaceCommandHandler(
    INetworkInterfaceRepository repository,
    ILogger<UpdateNetworkInterfaceCommandHandler> logger) : IRequestHandler<UpdateNetworkInterfaceCommand>
{
    public async Task Handle(UpdateNetworkInterfaceCommand request, CancellationToken cancellationToken)
    {
        var nic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Interface '{request.Id}' não encontrado.");

        nic.Update(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(nic.DeviceId, nic.Name, cancellationToken);
        if (existing is not null && existing.Id != nic.Id)
            throw new InvalidOperationException($"Já existe uma interface '{nic.Name}' neste device.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("NetworkInterface {InterfaceId} updated ({Name}).", nic.Id, nic.Name);
    }
}

public sealed record DeleteNetworkInterfaceCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteNetworkInterfaceCommandHandler(
    INetworkInterfaceRepository repository,
    ILogger<DeleteNetworkInterfaceCommandHandler> logger) : IRequestHandler<DeleteNetworkInterfaceCommand>
{
    public async Task Handle(DeleteNetworkInterfaceCommand request, CancellationToken cancellationToken)
    {
        var nic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Interface '{request.Id}' não encontrado.");

        // F2-T006: quando IpAddress existir com FK para NetworkInterface, aqui
        // passa a bloquear exclusão de interface com IPs (→ InvalidOperationException/409).
        repository.Remove(nic);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("NetworkInterface {InterfaceId} deleted ({Name}).", nic.Id, nic.Name);
    }
}
