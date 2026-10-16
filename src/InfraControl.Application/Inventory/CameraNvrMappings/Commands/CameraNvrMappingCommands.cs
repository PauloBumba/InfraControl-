using InfraControl.Application.Inventory.CameraNvrMappings.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.CameraNvrMappings.Commands;

// F30-T005c: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400 (domínio ou tipo do device),
// KeyNotFoundException → 404, InvalidOperationException → 409 (canal repetido
// no mesmo NVR — backstop de unicidade na migração).

public sealed record CreateCameraNvrMappingCommand(
    Guid CameraId,
    Guid NvrId,
    int ChannelNumber) : BaseCommand<CameraNvrMappingDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateCameraNvrMappingCommandHandler(
    ICameraNvrMappingRepository repository,
    IDeviceRepository deviceRepository,
    ILogger<CreateCameraNvrMappingCommandHandler> logger)
    : IRequestHandler<CreateCameraNvrMappingCommand, CameraNvrMappingDto>
{
    public async Task<CameraNvrMappingDto> Handle(
        CreateCameraNvrMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = CameraNvrMapping.Create(
            request.CameraId, request.NvrId, request.ChannelNumber);

        var camera = await deviceRepository.GetByIdAsync(mapping.CameraId, cancellationToken)
            ?? throw new KeyNotFoundException($"Câmera '{mapping.CameraId}' não encontrada.");
        if (camera.Type != DeviceType.Camera)
            throw new ArgumentException($"Device '{camera.Name}' não é uma câmera.");

        var nvr = await deviceRepository.GetByIdAsync(mapping.NvrId, cancellationToken)
            ?? throw new KeyNotFoundException($"NVR '{mapping.NvrId}' não encontrado.");
        if (nvr.Type != DeviceType.Nvr)
            throw new ArgumentException($"Device '{nvr.Name}' não é um NVR.");

        var existing = await repository.GetByNvrAndChannelAsync(
            mapping.NvrId, mapping.ChannelNumber, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException(
                $"Já existe um mapeamento do canal {mapping.ChannelNumber} no NVR '{nvr.Name}'.");

        await repository.AddAsync(mapping, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CameraNvrMapping {MappingId} created: camera {CameraId} → channel {Channel} on NVR {NvrId}.",
            mapping.Id, mapping.CameraId, mapping.ChannelNumber, mapping.NvrId);

        return new CameraNvrMappingDto(
            mapping.Id, mapping.CameraId, mapping.NvrId, mapping.ChannelNumber);
    }
}

public sealed record UpdateCameraNvrMappingCommand(
    Guid Id,
    Guid CameraId,
    Guid NvrId,
    int ChannelNumber) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateCameraNvrMappingCommandHandler(
    ICameraNvrMappingRepository repository,
    IDeviceRepository deviceRepository,
    ILogger<UpdateCameraNvrMappingCommandHandler> logger)
    : IRequestHandler<UpdateCameraNvrMappingCommand>
{
    public async Task Handle(
        UpdateCameraNvrMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Mapeamento '{request.Id}' não encontrado.");

        mapping.Update(request.CameraId, request.NvrId, request.ChannelNumber);

        var camera = await deviceRepository.GetByIdAsync(mapping.CameraId, cancellationToken)
            ?? throw new KeyNotFoundException($"Câmera '{mapping.CameraId}' não encontrada.");
        if (camera.Type != DeviceType.Camera)
            throw new ArgumentException($"Device '{camera.Name}' não é uma câmera.");

        var nvr = await deviceRepository.GetByIdAsync(mapping.NvrId, cancellationToken)
            ?? throw new KeyNotFoundException($"NVR '{mapping.NvrId}' não encontrado.");
        if (nvr.Type != DeviceType.Nvr)
            throw new ArgumentException($"Device '{nvr.Name}' não é um NVR.");

        var existing = await repository.GetByNvrAndChannelAsync(
            mapping.NvrId, mapping.ChannelNumber, cancellationToken);
        if (existing is not null && existing.Id != mapping.Id)
            throw new InvalidOperationException(
                $"Já existe um mapeamento do canal {mapping.ChannelNumber} no NVR '{nvr.Name}'.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CameraNvrMapping {MappingId} updated: camera {CameraId} → channel {Channel} on NVR {NvrId}.",
            mapping.Id, mapping.CameraId, mapping.ChannelNumber, mapping.NvrId);
    }
}

public sealed record DeleteCameraNvrMappingCommand(Guid Id)
    : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteCameraNvrMappingCommandHandler(
    ICameraNvrMappingRepository repository,
    ILogger<DeleteCameraNvrMappingCommandHandler> logger)
    : IRequestHandler<DeleteCameraNvrMappingCommand>
{
    public async Task Handle(
        DeleteCameraNvrMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Mapeamento '{request.Id}' não encontrado.");

        repository.Remove(mapping);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CameraNvrMapping {MappingId} deleted: camera {CameraId} channel {Channel} on NVR {NvrId}.",
            mapping.Id, mapping.CameraId, mapping.ChannelNumber, mapping.NvrId);
    }
}
