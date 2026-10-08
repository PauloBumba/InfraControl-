using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T003 — planejamento e resultado da Task (Plano de Execução §5) +
// ciclo de replanejamento (§13). Três comandos no molde da identificação
// (F23-T001): substituição com nulls limpando, guards pt-BR (400/404) e
// histórico "de → para" com no-op sem registro. O replanejo exige motivo
// e grava UMA entrada Replanned (datas alteradas + motivo) — precedente:
// replanejamento do PDI (docs/30 §14).

public sealed record UpdateTaskPlanningCommand(
    Guid Id,
    DateOnly? PlannedStart = null,
    DateOnly? PlannedEnd = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskPlanningCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<UpdateTaskPlanningCommandHandler> logger)
    : IRequestHandler<UpdateTaskPlanningCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        UpdateTaskPlanningCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        var beforeStart = task.PlannedStart;
        var beforeEnd = task.PlannedEnd;

        // Domínio: 400 pt-BR se fim < início.
        task.SetPlanning(request.PlannedStart, request.PlannedEnd);

        if (beforeStart == task.PlannedStart && beforeEnd == task.PlannedEnd)
            return await TaskDtoProjection.BuildAsync(
                task, assigneeRepository, deviceRepository, incidentRepository,
                cancellationToken); // no-op: sem save/histórico

        await repository.SaveChangesAsync(cancellationToken);

        // F7-T006: um registro por campo alterado, com antes → depois.
        if (beforeStart != task.PlannedStart)
            await historyWriter.RecordAsync(
                task.Id,
                TaskHistoryAction.PlanningChanged,
                $"início {TaskPlanningFormats.DateChange(
                    beforeStart, task.PlannedStart, "sem início")}",
                cancellationToken);

        if (beforeEnd != task.PlannedEnd)
            await historyWriter.RecordAsync(
                task.Id,
                TaskHistoryAction.PlanningChanged,
                $"fim {TaskPlanningFormats.DateChange(
                    beforeEnd, task.PlannedEnd, "sem fim")}",
                cancellationToken);

        logger.LogInformation(
            "Task {TaskId} planning: start {Start}, end {End} (user {User}).",
            task.Id, task.PlannedStart, task.PlannedEnd, currentUser.UserId);

        return await TaskDtoProjection.BuildAsync(
            task, assigneeRepository, deviceRepository, incidentRepository,
            cancellationToken);
    }
}

// F23-T003 — resultado da execução (§5): texto livre (≤2000) que descreve
// o que a task entregue; null/vazio limpa. Um registro ResultChanged por
// troca, com os textos truncados para caber no detail (≤200).

public sealed record UpdateTaskResultCommand(
    Guid Id,
    string? Result = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskResultCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<UpdateTaskResultCommandHandler> logger)
    : IRequestHandler<UpdateTaskResultCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        UpdateTaskResultCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        var before = task.Result;
        task.SetResult(request.Result); // 400 acima de TaskItem.ResultMaxLength

        if (before == task.Result)
            return await TaskDtoProjection.BuildAsync(
                task, assigneeRepository, deviceRepository, incidentRepository,
                cancellationToken); // no-op: sem save/histórico

        await repository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            task.Id,
            TaskHistoryAction.ResultChanged,
            TaskPlanningFormats.ResultChange(before, task.Result),
            cancellationToken);

        logger.LogInformation(
            "Task {TaskId} result updated (user {User}).",
            task.Id, currentUser.UserId);

        return await TaskDtoProjection.BuildAsync(
            task, assigneeRepository, deviceRepository, incidentRepository,
            cancellationToken);
    }
}

// F23-T003 — replanejamento (§13): ajusta início/fim/prazo COM motivo
// obrigatório em um único comando (ciclo do PDI: plano falhou → novo
// plano + justificativa). Sem mudança real → 400 (nada a replanejar);
// no máximo UMA entrada Replanned, "campos alterados — motivo: …"
// (≤200 do TaskHistory garantido por ReasonMaxLength = 80).

public sealed record ReplanTaskCommand(
    Guid Id,
    DateOnly? PlannedStart = null,
    DateOnly? PlannedEnd = null,
    DateOnly? DueDate = null,
    string Reason = "") : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public const int ReasonMaxLength = 80;

    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ReplanTaskCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<ReplanTaskCommandHandler> logger)
    : IRequestHandler<ReplanTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        ReplanTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException(
                "O motivo do replanejamento é obrigatório.",
                nameof(request.Reason));

        var reason = request.Reason.Trim();
        if (reason.Length > ReplanTaskCommand.ReasonMaxLength)
            throw new ArgumentException(
                $"O motivo do replanejamento pode ter no máximo "
                    + $"{ReplanTaskCommand.ReasonMaxLength} caracteres.",
                nameof(request.Reason));

        var beforeStart = task.PlannedStart;
        var beforeEnd = task.PlannedEnd;
        var beforeDueDate = task.DueDate;

        var changes = new List<string>(capacity: 3);
        if (beforeStart != request.PlannedStart)
            changes.Add($"início {TaskPlanningFormats.DateChange(
                beforeStart, request.PlannedStart, "sem início")}");
        if (beforeEnd != request.PlannedEnd)
            changes.Add($"fim {TaskPlanningFormats.DateChange(
                beforeEnd, request.PlannedEnd, "sem fim")}");
        if (beforeDueDate != request.DueDate)
            changes.Add($"prazo {TaskPlanningFormats.DateChange(
                beforeDueDate, request.DueDate, "sem prazo")}");

        if (changes.Count == 0)
            throw new ArgumentException(
                "Nada a replanejar: informe início, fim ou prazo "
                    + "diferentes do atual.",
                nameof(request.PlannedStart));

        task.SetPlanning(request.PlannedStart, request.PlannedEnd); // 400 se fim < início
        task.SetDueDate(request.DueDate);

        await repository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            task.Id,
            TaskHistoryAction.Replanned,
            $"{string.Join(", ", changes)} — motivo: {reason}",
            cancellationToken);

        logger.LogInformation(
            "Task {TaskId} replanned by {User}: {Changes} (reason: {Reason}).",
            task.Id, currentUser.UserId, string.Join("; ", changes), reason);

        return await TaskDtoProjection.BuildAsync(
            task, assigneeRepository, deviceRepository, incidentRepository,
            cancellationToken);
    }
}

// Formatação pt-BR dos detalhes de histórico (≤ TaskHistory.DetailMaxLength
// = 200 — truncagens garantem o limite mesmo com textos longos).

internal static class TaskPlanningFormats
{
    /// <summary>"de → para" de DateOnly? com rótulo para o lado nulo.</summary>
    public static string DateChange(
        DateOnly? before, DateOnly? after, string emptyLabel)
        => (before, after) switch
        {
            (not null, not null) => $"{before:yyyy-MM-dd} → {after:yyyy-MM-dd}",
            (null, not null) => $"{emptyLabel} → {after:yyyy-MM-dd}",
            (not null, null) => $"{before:yyyy-MM-dd} → {emptyLabel}",
            _ => $"{emptyLabel} → {emptyLabel}"
        };

    /// <summary>Resultado: textos truncados para caber no detail (≤200).</summary>
    public static string ResultChange(string? before, string? after)
        => (before, after) switch
        {
            (not null, not null) => $"{Truncate(before, 80)} → {Truncate(after, 80)}",
            (null, not null) => $"sem resultado → {Truncate(after, 150)}",
            (not null, null) => $"{Truncate(before, 150)} → removido",
            _ => "sem resultado → sem resultado"
        };

    public static string Truncate(string value, int max)
        => value.Length <= max ? value : $"{value[..(max - 1)]}…";
}

// Projeção TaskDto compartilhada pelos handlers de escrita que devolvem a
// task (planejamento/resultado/replanejo e subtarefas) — mesmos group
// joins de 1 id do GetTaskQuery/Search, com responsável resolvido
// (mesma cópia privada que os handlers de estado/identificação já fazem).

internal static class TaskDtoProjection
{
    public static async Task<TaskDto> BuildAsync(
        Domain.Entities.TaskItem task,
        ITaskAssigneeRepository assigneeRepository,
        ITaskDeviceRepository deviceRepository,
        ITaskIncidentRepository incidentRepository,
        CancellationToken cancellationToken)
    {
        var assignees = await assigneeRepository.GetUserIdsByTaskIdsAsync(
            [task.Id], cancellationToken);
        var devices = await deviceRepository.GetDeviceIdsByTaskIdsAsync(
            [task.Id], cancellationToken);
        var incidents = await incidentRepository.GetIncidentIdsByTaskIdsAsync(
            [task.Id], cancellationToken);
        var responsibleName = await CreateTaskCommandHandler
            .ResolveResponsibleNameAsync(
                assigneeRepository, task.ResponsibleId, cancellationToken);

        return TaskDto.From(
            task,
            assignees.TryGetValue(task.Id, out var a) ? a : null,
            devices.TryGetValue(task.Id, out var d) ? d : null,
            incidents.TryGetValue(task.Id, out var i) ? i : null,
            responsibleName);
    }
}
