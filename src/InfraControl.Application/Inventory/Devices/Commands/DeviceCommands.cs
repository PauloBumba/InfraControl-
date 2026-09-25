using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Devices.Commands;

// F2-T003: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (nome repetido, rack de outro local).

public sealed record CreateDeviceCommand(
    Guid LocationId,
    string Name,
    string Type,
    Guid? RackId = null,
    string? Ip = null,
    string? Mac = null,
    string? Serial = null,
    string? Model = null,
    string? Vendor = null,
    Guid? VlanId = null,
    string? Description = null) : BaseCommand<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateDeviceCommandHandler(
    IDeviceRepository repository,
    ILocationRepository locationRepository,
    IRackRepository rackRepository,
    IMonitoringCheckRepository checkRepository,
    ILogger<CreateDeviceCommandHandler> logger) : IRequestHandler<CreateDeviceCommand, DeviceDto>
{
    public async Task<DeviceDto> Handle(CreateDeviceCommand request, CancellationToken cancellationToken)
    {
        var type = ParseType(request.Type);
        var device = Device.Create(
            request.LocationId,
            request.Name,
            type,
            request.RackId,
            request.Ip,
            request.Mac,
            request.Serial,
            request.Model,
            request.Vendor,
            request.VlanId,
            request.Description);

        var locationExists = await locationRepository.GetByIdAsync(device.LocationId, cancellationToken);
        if (locationExists is null)
            throw new KeyNotFoundException($"Local '{device.LocationId}' não encontrado.");

        if (device.RackId is Guid rackId)
        {
            var rack = await rackRepository.GetByIdAsync(rackId, cancellationToken)
                ?? throw new KeyNotFoundException($"Rack '{rackId}' não encontrado.");

            if (rack.LocationId != device.LocationId)
                throw new InvalidOperationException("O rack não pertence ao local informado.");
        }

        var existing = await repository.GetByNameAsync(device.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Já existe um device com o nome '{device.Name}'.");

        await repository.AddAsync(device, cancellationToken);

        // F5-T001: materializa as instâncias MonitoringCheck do perfil
        // (ADR-014 §4) — mesmo SaveChanges do device.
        await checkRepository.SynchronizeFromProfileAsync(
            device.Id, device.MonitoringChecks, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Device {DeviceId} created ({Name}, {Type}) in location {LocationId}.",
            device.Id, device.Name, device.Type, device.LocationId);

        return ToDto(device);
    }

    internal static DeviceType ParseType(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || !Enum.TryParse(type.Trim(), ignoreCase: true, out DeviceType parsed))
            throw new ArgumentException($"Tipo de device inválido: '{type}'.", nameof(type));

        return parsed;
    }

    internal static DeviceDto ToDto(Device device) =>
        new(
            device.Id,
            device.LocationId,
            device.RackId,
            device.Type.ToString(),
            device.Name,
            device.Ip,
            device.Mac,
            device.Serial,
            device.Model,
            device.Vendor,
            device.VlanId,
            device.Description,
            device.MonitoringChecks.Select(check => check.ToString()).ToArray(),
            device.ProfileIsCustom);
}

public sealed record UpdateDeviceCommand(
    Guid Id,
    string Name,
    string Type,
    Guid? RackId = null,
    string? Ip = null,
    string? Mac = null,
    string? Serial = null,
    string? Model = null,
    string? Vendor = null,
    Guid? VlanId = null,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateDeviceCommandHandler(
    IDeviceRepository repository,
    ILocationRepository locationRepository,
    IRackRepository rackRepository,
    ILogger<UpdateDeviceCommandHandler> logger) : IRequestHandler<UpdateDeviceCommand>
{
    public async Task Handle(UpdateDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        var type = CreateDeviceCommandHandler.ParseType(request.Type);
        device.Update(
            request.Name,
            type,
            request.RackId,
            request.Ip,
            request.Mac,
            request.Serial,
            request.Model,
            request.Vendor,
            request.VlanId,
            request.Description);

        _ = await locationRepository.GetByIdAsync(device.LocationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Local '{device.LocationId}' não encontrado.");

        if (device.RackId is Guid rackId)
        {
            var rack = await rackRepository.GetByIdAsync(rackId, cancellationToken)
                ?? throw new KeyNotFoundException($"Rack '{rackId}' não encontrado.");

            if (rack.LocationId != device.LocationId)
                throw new InvalidOperationException("O rack não pertence ao local informado.");
        }

        var existing = await repository.GetByNameAsync(device.Name, cancellationToken);
        if (existing is not null && existing.Id != device.Id)
            throw new InvalidOperationException($"Já existe um device com o nome '{device.Name}'.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Device {DeviceId} updated ({Name}, {Type}).", device.Id, device.Name, device.Type);
    }
}

public sealed record DeleteDeviceCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteDeviceCommandHandler(
    IDeviceRepository repository,
    ILogger<DeleteDeviceCommandHandler> logger) : IRequestHandler<DeleteDeviceCommand>
{
    public async Task Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        // F3-T002: portas vêm antes (específicas de switch) — depois interfaces.
        if (await repository.HasPortsAsync(device.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir o device: existem portas vinculadas.");

        if (await repository.HasInterfacesAsync(device.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir o device: existem interfaces vinculadas.");

        repository.Remove(device);
        await repository.SaveChangesAsync(cancellationToken);

        // F2-T004/F3-T002: exclusão bloqueada acima quando há portas ou
        // interfaces; a FK Restrict no banco é o backstop. IPs (F2-T006)
        // bloqueiam exclusões de interface.
        logger.LogInformation("Device {DeviceId} deleted ({Name}).", device.Id, device.Name);
    }
}

// F2-T008 / ADR-014: override do perfil de checks (materialização default
// acontece no CreateDevice via DeviceProfileDefaults).
public sealed record UpdateDeviceProfileCommand(
    Guid Id,
    IReadOnlyCollection<CheckKind>? Checks = null,
    bool UseDefault = false) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateDeviceProfileCommandHandler(
    IDeviceRepository repository,
    IMonitoringCheckRepository checkRepository,
    ILogger<UpdateDeviceProfileCommandHandler> logger) : IRequestHandler<UpdateDeviceProfileCommand>
{
    public async Task Handle(UpdateDeviceProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.UseDefault)
        {
            if (request.Checks is { Count: > 0 })
                throw new ArgumentException(
                    "Informe os checks ou useDefault, não ambos.", nameof(request));
        }
        else if (request.Checks is null or { Count: 0 })
        {
            throw new ArgumentException("Informe os checks ou useDefault.", nameof(request));
        }

        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        if (request.UseDefault)
            device.ResetProfileToDefault();
        else
            device.ApplyCustomProfile(request.Checks!);

        // F5-T001: espelha a lista nova nas linhas MonitoringCheck (preserva
        // as existentes, adiciona as novas, remove as fora do perfil).
        await checkRepository.SynchronizeFromProfileAsync(
            device.Id, device.MonitoringChecks, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Device {DeviceId} profile updated (custom={Custom}, checks={Checks}).",
            device.Id, device.ProfileIsCustom, device.MonitoringChecks.Count);
    }
}
