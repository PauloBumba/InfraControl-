using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T001 — identificação básica da Task (Plano de Execução §5): prioridade,
// prazo (due date) e responsável principal em um comando único de
// SUBSTITUIÇÃO (PUT): o cliente envia os 3 campos; null em prazo/responsável
// limpa o valor. Prioridade é obrigatória (não existe "sem prioridade").
// Semântica e histórico por campo alterado (PriorityChanged/DueDateChanged/
// ResponsibleChanged) — antes/depois capturados no handler (TaskItem é
// silencioso). Sem FK para o usuário (precedente manager_id, ADR-029).

public sealed record SetTaskIdentificationCommand(
    Guid Id,
    string Priority,
    DateOnly? DueDate = null,
    Guid? ResponsibleId = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class SetTaskIdentificationCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<SetTaskIdentificationCommandHandler> logger)
    : IRequestHandler<SetTaskIdentificationCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        SetTaskIdentificationCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        if (string.IsNullOrWhiteSpace(request.Priority))
            throw new ArgumentException(
                "A prioridade é obrigatória. Use low, medium, high ou critical.",
                nameof(request.Priority));

        var priority = CreateTaskCommandHandler.ParsePriority(request.Priority);

        // Responsável precisa existir (404 pt-BR); null limpa o atual.
        if (request.ResponsibleId is { } responsibleId
            && !await assigneeRepository.UserExistsAsync(responsibleId, cancellationToken))
            throw new KeyNotFoundException(
                $"Usuário '{responsibleId}' não encontrado.");

        var beforePriority = task.Priority;
        var beforeDueDate = task.DueDate;
        var beforeResponsible = task.ResponsibleId;

        task.SetPriority(priority);
        task.SetDueDate(request.DueDate);
        task.SetResponsible(request.ResponsibleId);

        if (beforePriority == task.Priority
            && beforeDueDate == task.DueDate
            && beforeResponsible == task.ResponsibleId)
            return await BuildDtoAsync(task, cancellationToken); // no-op: sem save/histórico

        await repository.SaveChangesAsync(cancellationToken);

        // F7-T006: um registro por campo alterado, com antes → depois.
        if (beforePriority != task.Priority)
            await historyWriter.RecordAsync(
                task.Id,
                TaskHistoryAction.PriorityChanged,
                $"{beforePriority} → {task.Priority}",
                cancellationToken);

        if (beforeDueDate != task.DueDate)
            await historyWriter.RecordAsync(
                task.Id,
                TaskHistoryAction.DueDateChanged,
                FormatDueDateChange(beforeDueDate, task.DueDate),
                cancellationToken);

        if (beforeResponsible != task.ResponsibleId)
            await historyWriter.RecordAsync(
                task.Id,
                TaskHistoryAction.ResponsibleChanged,
                $"{beforeResponsible?.ToString() ?? "sem responsável"} → "
                    + $"{task.ResponsibleId?.ToString() ?? "removido"}",
                cancellationToken);

        logger.LogInformation(
            "Task {TaskId} identification: {Priority}, due {DueDate}, responsible {ResponsibleId} (user {User}).",
            task.Id, task.Priority, task.DueDate, task.ResponsibleId, currentUser.UserId);

        return await BuildDtoAsync(task, cancellationToken);
    }

    /// <summary>Detalhe pt-BR do histórico de prazo (≤200 — TaskHistory).</summary>
    internal static string FormatDueDateChange(DateOnly? before, DateOnly? after)
        => (before, after) switch
        {
            (null, null) => "sem prazo → sem prazo",
            (null, not null) => $"sem prazo → {after:yyyy-MM-dd}",
            (not null, null) => $"{before:yyyy-MM-dd} → sem prazo",
            _ => $"{before:yyyy-MM-dd} → {after:yyyy-MM-dd}"
        };

    private async Task<TaskDto> BuildDtoAsync(
        Domain.Entities.TaskItem task, CancellationToken cancellationToken)
    {
        // Group joins de 1 id — mesmos repositórios do GetTaskQuery/Search.
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
