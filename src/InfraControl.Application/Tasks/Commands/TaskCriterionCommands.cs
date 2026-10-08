using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T003 — critérios de aceite da Task (Plano de Execução §12): CRUD no
// molde dos comentários (F7-T005) — 404 pt-BR para task/critério
// inexistente, 400 do domínio para descrição vazia/acima de 300, um
// registro de histórico por mudança (CriterionAdded/Updated/Removed) com
// a descrição truncada para o limit de 200 do TaskHistory.

public sealed record AddTaskCriterionCommand(
    Guid TaskId,
    string Description) : BaseCommand<TaskCriterionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AddTaskCriterionCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskCriterionRepository criterionRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<AddTaskCriterionCommandHandler> logger)
    : IRequestHandler<AddTaskCriterionCommand, TaskCriterionDto>
{
    public async Task<TaskCriterionDto> Handle(
        AddTaskCriterionCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var criterion = TaskCriterion.Create(request.TaskId, request.Description);

        await criterionRepository.AddAsync(criterion, cancellationToken);
        await criterionRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            criterion.TaskId,
            TaskHistoryAction.CriterionAdded,
            TaskPlanningFormats.Truncate(criterion.Description, 190),
            cancellationToken);

        logger.LogInformation(
            "Criterion {CriterionId} added to task {TaskId}.",
            criterion.Id, criterion.TaskId);

        return ToDto(criterion);
    }

    internal static TaskCriterionDto ToDto(TaskCriterion criterion) =>
        new(
            criterion.Id,
            criterion.TaskId,
            criterion.Description,
            criterion.IsDone,
            criterion.DoneAt,
            criterion.CreateDateTime);
}

// ADR-030 §1 — PUT do critério (Operate): descrição e/ou marcação de
// atendido em um mesmo comando; null = mantém. No-op (nada mudou) não
// salva nem grava histórico.

public sealed record UpdateTaskCriterionCommand(
    Guid Id,
    string? Description = null,
    bool? Done = null) : BaseCommand<TaskCriterionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskCriterionCommandHandler(
    ITaskCriterionRepository criterionRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UpdateTaskCriterionCommandHandler> logger)
    : IRequestHandler<UpdateTaskCriterionCommand, TaskCriterionDto>
{
    public async Task<TaskCriterionDto> Handle(
        UpdateTaskCriterionCommand request, CancellationToken cancellationToken)
    {
        var criterion = await criterionRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Critério '{request.Id}' não encontrado.");

        var beforeDescription = criterion.Description;
        var beforeDone = criterion.IsDone;

        if (request.Description is { } description)
            criterion.EditDescription(description); // domínio: 400 se vazio/>300
        if (request.Done is { } done)
            criterion.SetDone(done);

        var descriptionChanged = beforeDescription != criterion.Description;
        var doneChanged = beforeDone != criterion.IsDone;

        if (!descriptionChanged && !doneChanged)
            return AddTaskCriterionCommandHandler.ToDto(criterion); // no-op

        await criterionRepository.SaveChangesAsync(cancellationToken);

        // F7-T006: um registro por aspecto alterado.
        if (descriptionChanged)
            await historyWriter.RecordAsync(
                criterion.TaskId,
                TaskHistoryAction.CriterionUpdated,
                $"descrição: {TaskPlanningFormats.Truncate(beforeDescription, 70)}"
                    + $" → {TaskPlanningFormats.Truncate(criterion.Description, 70)}",
                cancellationToken);

        if (doneChanged)
            await historyWriter.RecordAsync(
                criterion.TaskId,
                TaskHistoryAction.CriterionUpdated,
                criterion.IsDone
                    ? "marcado como atendido: "
                        + TaskPlanningFormats.Truncate(criterion.Description, 170)
                    : "desmarcado: "
                        + TaskPlanningFormats.Truncate(criterion.Description, 185),
                cancellationToken);

        logger.LogInformation(
            "Criterion {CriterionId} updated (done: {Done}).",
            criterion.Id, criterion.IsDone);

        return AddTaskCriterionCommandHandler.ToDto(criterion);
    }
}

public sealed record DeleteTaskCriterionCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteTaskCriterionCommandHandler(
    ITaskCriterionRepository criterionRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<DeleteTaskCriterionCommandHandler> logger)
    : IRequestHandler<DeleteTaskCriterionCommand>
{
    public async Task Handle(
        DeleteTaskCriterionCommand request, CancellationToken cancellationToken)
    {
        var criterion = await criterionRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Critério '{request.Id}' não encontrado.");

        // Histórico antes do Remove — o registro sobrevive à exclusão.
        await historyWriter.RecordAsync(
            criterion.TaskId,
            TaskHistoryAction.CriterionRemoved,
            TaskPlanningFormats.Truncate(criterion.Description, 190),
            cancellationToken);

        criterion.Delete(); // ADR-030 §5 — TaskCriterionDeletedDomainEvent
        criterionRepository.Remove(criterion);
        await criterionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Criterion {CriterionId} deleted from task {TaskId}.",
            criterion.Id, criterion.TaskId);
    }
}
