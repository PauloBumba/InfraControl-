using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T004: vínculo Task ↔ Incident — task/incidente inexistente → 404
// pt-BR; duplicidade → 409 pt-BR.

public sealed record LinkTaskIncidentCommand(
    Guid TaskId,
    Guid IncidentId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkTaskIncidentCommandHandler(
    ITaskItemRepository taskRepository,
    IIncidentRepository incidentRepository,
    ITaskIncidentRepository taskIncidentRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<LinkTaskIncidentCommandHandler> logger)
    : IRequestHandler<LinkTaskIncidentCommand>
{
    public async Task Handle(
        LinkTaskIncidentCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        _ = await incidentRepository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Incidente '{request.IncidentId}' não encontrado.");

        if (await taskIncidentRepository.ExistsAsync(
                request.TaskId, request.IncidentId, cancellationToken))
            throw new InvalidOperationException(
                "O incidente já está vinculado à task.");

        await taskIncidentRepository.AddAsync(
            TaskIncident.Create(request.TaskId, request.IncidentId), cancellationToken);
        await taskIncidentRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.IncidentLinked,
            request.IncidentId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} linked to incident {IncidentId}.",
            request.TaskId, request.IncidentId);
    }
}

public sealed record UnlinkTaskIncidentCommand(
    Guid TaskId,
    Guid IncidentId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkTaskIncidentCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskIncidentRepository taskIncidentRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UnlinkTaskIncidentCommandHandler> logger)
    : IRequestHandler<UnlinkTaskIncidentCommand>
{
    public async Task Handle(
        UnlinkTaskIncidentCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var link = await taskIncidentRepository.FindAsync(
                request.TaskId, request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo da task '{request.TaskId}' com o incidente "
                + $"'{request.IncidentId}' não encontrado.");

        taskIncidentRepository.Remove(link);
        await taskIncidentRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.IncidentUnlinked,
            request.IncidentId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} unlinked from incident {IncidentId}.",
            request.TaskId, request.IncidentId);
    }
}
