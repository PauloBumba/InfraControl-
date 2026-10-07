using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T004/P4b — impedimentos estruturados da Task (Plano de Execução §10):
// CRUD no molde dos critérios (F23-T003) — 404 pt-BR para task/impedimento
// inexistente, 400 do domínio para descrição vazia/acima de 300, um
// registro de histórico por mudança (ImpedimentAdded/Updated/Resolved/
// Reopened/Removed) com a descrição truncada para o limit de 200 do
// TaskHistory. O estado do workflow (Blocked) continua independente:
// registrar impedimento NÃO muda estado (mudança de workflow = ADR).

public sealed record AddTaskImpedimentCommand(
    Guid TaskId,
    string Description) : BaseCommand<TaskImpedimentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AddTaskImpedimentCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskImpedimentRepository impedimentRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<AddTaskImpedimentCommandHandler> logger)
    : IRequestHandler<AddTaskImpedimentCommand, TaskImpedimentDto>
{
    public async Task<TaskImpedimentDto> Handle(
        AddTaskImpedimentCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var impediment = TaskImpediment.Create(request.TaskId, request.Description);

        await impedimentRepository.AddAsync(impediment, cancellationToken);
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            impediment.TaskId,
            TaskHistoryAction.ImpedimentAdded,
            TaskPlanningFormats.Truncate(impediment.Description, 190),
            cancellationToken);

        logger.LogInformation(
            "Impediment {ImpedimentId} added to task {TaskId}.",
            impediment.Id, impediment.TaskId);

        return ToDto(impediment);
    }

    internal static TaskImpedimentDto ToDto(TaskImpediment impediment) =>
        new(
            impediment.Id,
            impediment.TaskId,
            impediment.Description,
            impediment.CreateDateTime,
            impediment.ResolvedAt);
}

// ADR-030 §1 — PUT do impedimento (Operate): descrição e/ou resolução em
// um mesmo comando; null = mantém. No-op (nada mudou) não salva nem grava
// histórico.

public sealed record UpdateTaskImpedimentCommand(
    Guid Id,
    string? Description = null,
    bool? Resolved = null) : BaseCommand<TaskImpedimentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskImpedimentCommandHandler(
    ITaskImpedimentRepository impedimentRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UpdateTaskImpedimentCommandHandler> logger)
    : IRequestHandler<UpdateTaskImpedimentCommand, TaskImpedimentDto>
{
    public async Task<TaskImpedimentDto> Handle(
        UpdateTaskImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        var beforeDescription = impediment.Description;
        var beforeResolved = impediment.IsResolved;

        if (request.Description is { } description)
            impediment.EditDescription(description); // domínio: 400 se vazio/>300
        if (request.Resolved is { } resolved)
            impediment.SetResolved(resolved);

        var descriptionChanged = beforeDescription != impediment.Description;
        var resolvedChanged = beforeResolved != impediment.IsResolved;

        if (!descriptionChanged && !resolvedChanged)
            return AddTaskImpedimentCommandHandler.ToDto(impediment); // no-op

        await impedimentRepository.SaveChangesAsync(cancellationToken);

        // F7-T006: um registro por aspecto alterado.
        if (descriptionChanged)
            await historyWriter.RecordAsync(
                impediment.TaskId,
                TaskHistoryAction.ImpedimentUpdated,
                $"descrição: {TaskPlanningFormats.Truncate(beforeDescription, 70)}"
                    + $" → {TaskPlanningFormats.Truncate(impediment.Description, 70)}",
                cancellationToken);

        if (resolvedChanged)
            await historyWriter.RecordAsync(
                impediment.TaskId,
                impediment.IsResolved
                    ? TaskHistoryAction.ImpedimentResolved
                    : TaskHistoryAction.ImpedimentReopened,
                (impediment.IsResolved ? "resolvido: " : "reaberto: ")
                    + TaskPlanningFormats.Truncate(impediment.Description, 185),
                cancellationToken);

        logger.LogInformation(
            "Impediment {ImpedimentId} updated (resolved: {Resolved}).",
            impediment.Id, impediment.IsResolved);

        return AddTaskImpedimentCommandHandler.ToDto(impediment);
    }
}

public sealed record DeleteTaskImpedimentCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteTaskImpedimentCommandHandler(
    ITaskImpedimentRepository impedimentRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<DeleteTaskImpedimentCommandHandler> logger)
    : IRequestHandler<DeleteTaskImpedimentCommand>
{
    public async Task Handle(
        DeleteTaskImpedimentCommand request, CancellationToken cancellationToken)
    {
        var impediment = await impedimentRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Impedimento '{request.Id}' não encontrado.");

        // Histórico antes do Remove — o registro sobrevive à exclusão.
        await historyWriter.RecordAsync(
            impediment.TaskId,
            TaskHistoryAction.ImpedimentRemoved,
            TaskPlanningFormats.Truncate(impediment.Description, 190),
            cancellationToken);

        impediment.Delete(); // ADR-030 §5 — TaskImpedimentDeletedDomainEvent
        impedimentRepository.Remove(impediment);
        await impedimentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Impediment {ImpedimentId} deleted from task {TaskId}.",
            impediment.Id, impediment.TaskId);
    }
}
