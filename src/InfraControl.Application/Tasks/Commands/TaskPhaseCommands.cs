using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T004/P4d — fases dentro da task (Plano de Execução §7): etapas
// nomeadas por task (design escolhido no checkpoint — sem conjunto
// fixo). 404 pt-BR para task/fase inexistente, 400 do domínio para nome
// vazio/>120, um registro de histórico por aspecto alterado
// (PhaseAdded/Updated/CurrentChanged/Completed/Reopened/Removed) com o
// nome truncado para o limit de 200 do TaskHistory. Ponteiro de "fase
// atual" é único por task (o PUT limpa os irmãos num mesmo save) e o
// no-op não grava nada. Ordem: gaps de posição são aceitos (leitura
// ordena; novas fases entram em max+1).

public sealed record AddTaskPhaseCommand(
    Guid TaskId,
    string Name) : BaseCommand<TaskPhaseDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AddTaskPhaseCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskPhaseRepository phaseRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<AddTaskPhaseCommandHandler> logger)
    : IRequestHandler<AddTaskPhaseCommand, TaskPhaseDto>
{
    public async Task<TaskPhaseDto> Handle(
        AddTaskPhaseCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var existing = await phaseRepository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);
        var position = existing.Count == 0
            ? 0
            : existing.Max(phase => phase.Position) + 1;

        // A primeira fase já nasce como a atual — sem histórico extra
        // (é consequência do próprio cadastro).
        var phase = TaskPhase.Create(
            request.TaskId, request.Name, position,
            isCurrent: existing.Count == 0);

        await phaseRepository.AddAsync(phase, cancellationToken);
        await phaseRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            phase.TaskId,
            TaskHistoryAction.PhaseAdded,
            TaskPlanningFormats.Truncate(phase.Name, 190),
            cancellationToken);

        logger.LogInformation(
            "Phase {PhaseId} (position {Position}) added to task {TaskId}.",
            phase.Id, phase.Position, phase.TaskId);

        return ToDto(phase);
    }

    internal static TaskPhaseDto ToDto(TaskPhase phase) =>
        new(
            phase.Id,
            phase.TaskId,
            phase.Name,
            phase.Position,
            phase.IsCurrent,
            phase.CompletedAt);
}

// ADR-030 §1 — PUT da fase (Operate): nome e/ou ponteiro de fase atual
// e/ou conclusão em um mesmo comando; null = mantém. Trocar o ponteiro
// limpa os irmãos (máx. 1 atual por task). No-op = 200 sem histórico.

public sealed record UpdateTaskPhaseCommand(
    Guid Id,
    string? Name = null,
    bool? Current = null,
    bool? Completed = null) : BaseCommand<TaskPhaseDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskPhaseCommandHandler(
    ITaskPhaseRepository phaseRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UpdateTaskPhaseCommandHandler> logger)
    : IRequestHandler<UpdateTaskPhaseCommand, TaskPhaseDto>
{
    public async Task<TaskPhaseDto> Handle(
        UpdateTaskPhaseCommand request, CancellationToken cancellationToken)
    {
        var phase = await phaseRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Fase '{request.Id}' não encontrada.");

        var beforeName = phase.Name;
        var beforeCurrent = phase.IsCurrent;
        var beforeCompleted = phase.IsCompleted;

        if (request.Name is { } name)
            phase.Rename(name); // domínio: 400 se vazio/>120

        if (request.Current == true && !phase.IsCurrent)
        {
            // Ponteiro único: limpa os irmãos no mesmo save.
            var siblings = await phaseRepository.GetByTaskIdAsync(
                phase.TaskId, cancellationToken);
            foreach (var sibling in siblings.Where(
                         other => other.Id != phase.Id && other.IsCurrent))
                sibling.SetCurrent(false);
            phase.SetCurrent(true);
        }
        else if (request.Current == false && phase.IsCurrent)
        {
            phase.SetCurrent(false);
        }

        if (request.Completed == true && !phase.IsCompleted)
            phase.Complete();
        else if (request.Completed == false && phase.IsCompleted)
            phase.Reopen();

        var nameChanged = beforeName != phase.Name;
        var currentChanged = beforeCurrent != phase.IsCurrent;
        var completedChanged = beforeCompleted != phase.IsCompleted;

        if (!nameChanged && !currentChanged && !completedChanged)
            return AddTaskPhaseCommandHandler.ToDto(phase); // no-op

        await phaseRepository.SaveChangesAsync(cancellationToken);

        // F7-T006: um registro por aspecto alterado.
        if (nameChanged)
            await historyWriter.RecordAsync(
                phase.TaskId,
                TaskHistoryAction.PhaseUpdated,
                $"nome: {TaskPlanningFormats.Truncate(beforeName, 70)}"
                    + $" → {TaskPlanningFormats.Truncate(phase.Name, 70)}",
                cancellationToken);

        if (currentChanged)
            await historyWriter.RecordAsync(
                phase.TaskId,
                TaskHistoryAction.PhaseCurrentChanged,
                TaskPlanningFormats.Truncate(phase.Name, 190),
                cancellationToken);

        if (completedChanged)
            await historyWriter.RecordAsync(
                phase.TaskId,
                phase.IsCompleted
                    ? TaskHistoryAction.PhaseCompleted
                    : TaskHistoryAction.PhaseReopened,
                TaskPlanningFormats.Truncate(phase.Name, 190),
                cancellationToken);

        logger.LogInformation(
            "Phase {PhaseId} updated (current: {Current}, completed: {Completed}).",
            phase.Id, phase.IsCurrent, phase.IsCompleted);

        return AddTaskPhaseCommandHandler.ToDto(phase);
    }
}

public sealed record DeleteTaskPhaseCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteTaskPhaseCommandHandler(
    ITaskPhaseRepository phaseRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<DeleteTaskPhaseCommandHandler> logger)
    : IRequestHandler<DeleteTaskPhaseCommand>
{
    public async Task Handle(
        DeleteTaskPhaseCommand request, CancellationToken cancellationToken)
    {
        var phase = await phaseRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Fase '{request.Id}' não encontrada.");

        // Histórico antes do Remove — o registro sobrevive à exclusão.
        // O ponteiro de "atual" simplesmente some com a fase (nenhum
        // irmão é promovido automaticamente — sem mágica).
        await historyWriter.RecordAsync(
            phase.TaskId,
            TaskHistoryAction.PhaseRemoved,
            TaskPlanningFormats.Truncate(phase.Name, 190),
            cancellationToken);

        phase.Delete(); // ADR-030 §5 — TaskPhaseDeletedDomainEvent
        phaseRepository.Remove(phase);
        await phaseRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Phase {PhaseId} deleted from task {TaskId}.",
            phase.Id, phase.TaskId);
    }
}
