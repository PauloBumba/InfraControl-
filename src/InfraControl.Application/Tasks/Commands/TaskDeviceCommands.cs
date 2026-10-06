using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T003: vínculo Task ↔ Device — task/device inexistente → 404 pt-BR;
// duplicidade → 409 pt-BR.

public sealed record LinkTaskDeviceCommand(
    Guid TaskId,
    Guid DeviceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkTaskDeviceCommandHandler(
    ITaskItemRepository taskRepository,
    IDeviceRepository deviceRepository,
    ITaskDeviceRepository taskDeviceRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<LinkTaskDeviceCommandHandler> logger)
    : IRequestHandler<LinkTaskDeviceCommand>
{
    public async Task Handle(
        LinkTaskDeviceCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        _ = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        if (await taskDeviceRepository.ExistsAsync(
                request.TaskId, request.DeviceId, cancellationToken))
            throw new InvalidOperationException(
                "O device já está vinculado à task.");

        await taskDeviceRepository.AddAsync(
            TaskDevice.Create(request.TaskId, request.DeviceId), cancellationToken);
        await taskDeviceRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.DeviceLinked,
            request.DeviceId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} linked to device {DeviceId}.",
            request.TaskId, request.DeviceId);
    }
}

public sealed record UnlinkTaskDeviceCommand(
    Guid TaskId,
    Guid DeviceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkTaskDeviceCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskDeviceRepository taskDeviceRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UnlinkTaskDeviceCommandHandler> logger)
    : IRequestHandler<UnlinkTaskDeviceCommand>
{
    public async Task Handle(
        UnlinkTaskDeviceCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var link = await taskDeviceRepository.FindAsync(
                request.TaskId, request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo da task '{request.TaskId}' com o device "
                + $"'{request.DeviceId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — junçãoDeletedDomainEvent na mesma transação

        taskDeviceRepository.Remove(link);
        await taskDeviceRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.DeviceUnlinked,
            request.DeviceId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} unlinked from device {DeviceId}.",
            request.TaskId, request.DeviceId);
    }
}
