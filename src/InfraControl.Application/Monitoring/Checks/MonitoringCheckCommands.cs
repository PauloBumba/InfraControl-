using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Monitoring.Checks;

// ADR-030 §1 — CRUD completo de MonitoringCheck. As checks nascem do
// DeviceProfile (F5-T001) e a sincronização de perfil preserva as linhas
// existentes — um Create manual cobre o caso "ligar check sem trocar de
// perfil" e um Delete sai do device até o próximo sync recriá-lo (registro
// aqui: o sync é a fonte do perfil). Mapeamento: ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409.

/// <summary>Adiciona uma check ao device (upsert manual — duplicidade = 409).</summary>
public sealed record CreateMonitoringCheckCommand(
    Guid DeviceId,
    string Check,
    bool Enabled = true) : BaseCommand<MonitoringCheckDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateMonitoringCheckCommandHandler(
    IMonitoringCheckRepository checkRepository,
    IDeviceRepository deviceRepository,
    ILogger<CreateMonitoringCheckCommandHandler> logger)
    : IRequestHandler<CreateMonitoringCheckCommand, MonitoringCheckDto>
{
    public async Task<MonitoringCheckDto> Handle(
        CreateMonitoringCheckCommand request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.DeviceId}' não encontrado.");

        var kind = RunCheckCommandHandler.ParseCheck(request.Check);

        var existing = await checkRepository.GetByDeviceAndKindAsync(
            device.Id, kind, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Device já possui a check '{kind}'.");

        var check = MonitoringCheck.Create(device.Id, kind, request.Enabled);
        await checkRepository.AddAsync(check, cancellationToken);
        await checkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "MonitoringCheck {CheckId} ({Kind}) created for device {DeviceId}.",
            check.Id, kind, device.Id);

        return new MonitoringCheckDto(
            check.Id, check.DeviceId, check.Kind.ToString(), check.Enabled);
    }
}

/// <summary>Toggle de habilitação da check (único campo mutável).</summary>
public sealed record UpdateMonitoringCheckCommand(Guid Id, bool Enabled)
    : BaseCommand<MonitoringCheckDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateMonitoringCheckCommandHandler(
    IMonitoringCheckRepository checkRepository)
    : IRequestHandler<UpdateMonitoringCheckCommand, MonitoringCheckDto>
{
    public async Task<MonitoringCheckDto> Handle(
        UpdateMonitoringCheckCommand request,
        CancellationToken cancellationToken)
    {
        var check = await checkRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Check '{request.Id}' não encontrada.");

        check.SetEnabled(request.Enabled);
        await checkRepository.SaveChangesAsync(cancellationToken);

        return new MonitoringCheckDto(
            check.Id, check.DeviceId, check.Kind.ToString(), check.Enabled);
    }
}

/// <summary>Remove a check do device (o sync do profile pode recriá-la).</summary>
public sealed record DeleteMonitoringCheckCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteMonitoringCheckCommandHandler(
    IMonitoringCheckRepository checkRepository,
    ILogger<DeleteMonitoringCheckCommandHandler> logger)
    : IRequestHandler<DeleteMonitoringCheckCommand>
{
    public async Task Handle(
        DeleteMonitoringCheckCommand request,
        CancellationToken cancellationToken)
    {
        var check = await checkRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Check '{request.Id}' não encontrada.");

        checkRepository.Remove(check);
        await checkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "MonitoringCheck {CheckId} ({Kind}) deleted from device {DeviceId}.",
            check.Id, check.Kind, check.DeviceId);
    }
}
