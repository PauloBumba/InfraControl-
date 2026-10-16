using InfraControl.Application.Tasks.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T001: CRUD base da Task — erros pt-BR sobem no envelope via
// GlobalExceptionHandler (ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409).
// Transições de estado = F7-T007 (Workflow); vínculos = F7-T002→T004.

public sealed record CreateTaskCommand(
    string Title,
    string Kind,
    string? Description = null,
    string? Priority = null,
    DateOnly? DueDate = null,
    Guid? ResponsibleId = null,
    Guid? ParentTaskId = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateTaskCommandHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskHistoryWriter historyWriter,
    ICurrentUserContext currentUser,
    ILogger<CreateTaskCommandHandler> logger)
    : IRequestHandler<CreateTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var priority = ParsePriority(request.Priority);

        // F31-T001: o criador vem do usuário autenticado (mesmo critério
        // do CreateProjectCommandHandler) — sem sessão não cria task.
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } createdById)
            throw new UnauthorizedAccessException(
                "Usuário autenticado não identificado para criação da task.");

        // F23-T001: responsável precisa existir (404 pt-BR) — o vínculo
        // com assignees é opcional e independente.
        if (request.ResponsibleId is { } responsibleId
            && !await assigneeRepository.UserExistsAsync(responsibleId, cancellationToken))
            throw new KeyNotFoundException(
                $"Usuário '{responsibleId}' não encontrado.");

        // F23-T004/P4a: criação como subtarefa — pai precisa existir e
        // não pode ser ele mesmo subtarefa (profundidade ≤ 2).
        TaskItem? parent = null;
        if (request.ParentTaskId is { } parentId)
        {
            parent = await repository.GetByIdAsync(parentId, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Task '{parentId}' não encontrada.");
            if (parent.ParentTaskId != null)
                throw new ArgumentException(
                    "Subtarefas não podem ter subtarefas.",
                    nameof(request.ParentTaskId));
        }

        var task = TaskItem.Create(
            request.Title,
            ParseKind(request.Kind),
            request.Description,
            priority,
            request.DueDate,
            request.ResponsibleId,
            createdById);
        task.SetParent(parent?.Id);

        await repository.AddAsync(task, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // F7-T006: histórico com o usuário logado como actor.
        await historyWriter.RecordAsync(
            task.Id, TaskHistoryAction.Created, cancellationToken: cancellationToken);

        // F23-T004/P4a: o pai também registra a nova subtarefa.
        if (parent != null)
            await historyWriter.RecordAsync(
                parent.Id, TaskHistoryAction.SubtaskAdded, task.Title,
                cancellationToken);

        logger.LogInformation(
            "Task {TaskId} created ({Kind} — {Title}){Parent} by {User}.",
            task.Id, task.Kind, task.Title,
            parent == null ? "" : $" [subtask of {parent.Id}]",
            createdById);

        var responsibleName = await ResolveResponsibleNameAsync(
            assigneeRepository, task.ResponsibleId, cancellationToken);
        var createdByName = await ResolveResponsibleNameAsync(
            assigneeRepository, task.CreatedById, cancellationToken);

        return TaskDto.From(
            task, responsibleName: responsibleName,
            parentTitle: parent?.Title, createdByName: createdByName);
    }

    /// <summary>F23-T001: null/vazio = prioridade padrão Medium.</summary>
    internal static TaskPriority ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return TaskPriority.Medium;

        if (!Enum.TryParse(priority.Trim(), ignoreCase: true, out TaskPriority parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Prioridade inválida: '{priority}'. Use low, medium, high ou critical.",
                nameof(priority));

        return parsed;
    }

    /// <summary>
    /// Nome do responsável (F23-T001) — compartilhado pelo Get/Search e
    /// pelo SetTaskIdentification; sem responsável = null.
    /// </summary>
    internal static async Task<string?> ResolveResponsibleNameAsync(
        ITaskAssigneeRepository assigneeRepository,
        Guid? responsibleId,
        CancellationToken cancellationToken)
    {
        if (responsibleId is not { } id)
            return null;

        var names = await assigneeRepository.GetUserNamesAsync(
            [id], cancellationToken);
        return names.TryGetValue(id, out var name) ? name : null;
    }

    internal static TaskKind ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)
            || !Enum.TryParse(kind.Trim(), ignoreCase: true, out TaskKind parsed))
            throw new ArgumentException(
                $"Tipo inválido: '{kind}'. Use manual, incident, alert, monitoring, "
                + "discovery, maintenance, change, preventive, corrective, project ou system.",
                nameof(kind));

        return parsed;
    }
}

public sealed record UpdateTaskCommand(
    Guid Id,
    string Title,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateTaskCommandHandler(
    ITaskItemRepository repository,
    ITaskHistoryWriter historyWriter,
    ILogger<UpdateTaskCommandHandler> logger)
    : IRequestHandler<UpdateTaskCommand>
{
    public async Task Handle(
        UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        task.Update(request.Title, request.Description);
        await repository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            task.Id, TaskHistoryAction.Updated, task.Title, cancellationToken);

        logger.LogInformation("Task {TaskId} updated ({Title}).", task.Id, task.Title);
    }
}

public sealed record DeleteTaskCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteTaskCommandHandler(
    ITaskItemRepository repository,
    ILogger<DeleteTaskCommandHandler> logger)
    : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(
        DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        // F7-T002→T005 passam a bloquear aqui (pré-check) quando houver
        // assignees/devices/incidents/comentários; FKs Restrict são o
        // backstop (padrão Locations/DeviceCredentials).
        task.Delete(); // ADR-030 §5 — TaskItemDeletedDomainEvent na mesma transação
        repository.Remove(task);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Task {TaskId} deleted ({Title}).", task.Id, task.Title);
    }
}
