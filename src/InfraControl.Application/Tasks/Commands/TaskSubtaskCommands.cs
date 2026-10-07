using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T004/P4a — subtarefas (spec "subtarefas, progresso"): mover ou
// desvincular o pai de uma task em um comando único. Guards pt-BR:
// 404 (task/pai), 400 (auto-relação, pai que é ele mesmo subtarefa),
// no-op sem histórico. Histórico em 3 frentes: no filho
// (ParentChanged "de → para" com títulos truncados ≤200) e em cada pai
// afetado (SubtaskAdded/SubtaskRemoved com o título do filho).

public sealed record SetTaskParentCommand(
    Guid Id,
    Guid? ParentId = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class SetTaskParentCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<SetTaskParentCommandHandler> logger)
    : IRequestHandler<SetTaskParentCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        SetTaskParentCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        if (task.ParentTaskId == request.ParentId)
            return await TaskDtoProjection.BuildAsync(
                task, assigneeRepository, deviceRepository, incidentRepository,
                cancellationToken); // no-op: sem save/histórico

        // Guard explícito antes das consultas (mensagem clara; o domínio
        // repete o check em SetParent como backstop).
        if (request.ParentId == task.Id)
            throw new ArgumentException(
                "Uma task não pode ser própria subtarefa.",
                nameof(request.ParentId));

        var beforeParentId = task.ParentTaskId;
        TaskItem? oldParent = null;
        if (beforeParentId is { } oldParentId)
            oldParent = await repository.GetByIdAsync(oldParentId, cancellationToken);

        TaskItem? newParent = null;
        if (request.ParentId is { } newParentId)
        {
            newParent = await repository.GetByIdAsync(newParentId, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Task '{newParentId}' não encontrada.");
            if (newParent.ParentTaskId != null)
                throw new ArgumentException(
                    "Subtarefas não podem ter subtarefas.",
                    nameof(request.ParentId));
        }

        // Lados do "de → para" (títulos ≤120 → truncados em 95; detalhe
        // final ≤193, dentro do limit de 200 do TaskHistory).
        var beforeLabel = beforeParentId is null
            ? "sem pai"
            : TaskPlanningFormats.Truncate(
                oldParent?.Title ?? "task desconhecida", 95);
        var afterLabel = request.ParentId is null
            ? "sem pai"
            : TaskPlanningFormats.Truncate(newParent!.Title, 95);

        task.SetParent(request.ParentId);
        await repository.SaveChangesAsync(cancellationToken);

        // No filho: vínculo alterado.
        await historyWriter.RecordAsync(
            task.Id,
            TaskHistoryAction.ParentChanged,
            $"{beforeLabel} → {afterLabel}",
            cancellationToken);

        // Nos pais afetados: ganho/perda da subtarefa (título ≤120).
        if (oldParent != null)
            await historyWriter.RecordAsync(
                oldParent.Id, TaskHistoryAction.SubtaskRemoved, task.Title,
                cancellationToken);
        if (newParent != null)
            await historyWriter.RecordAsync(
                newParent.Id, TaskHistoryAction.SubtaskAdded, task.Title,
                cancellationToken);

        logger.LogInformation(
            "Task {TaskId} parent: {Before} → {After} (user {User}).",
            task.Id, beforeParentId, request.ParentId, currentUser.UserId);

        return await TaskDtoProjection.BuildAsync(
            task, assigneeRepository, deviceRepository, incidentRepository,
            cancellationToken);
    }
}
