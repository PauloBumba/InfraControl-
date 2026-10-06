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
    string? Description = null) : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateTaskCommandHandler(
    ITaskItemRepository repository,
    ITaskHistoryWriter historyWriter,
    ILogger<CreateTaskCommandHandler> logger)
    : IRequestHandler<CreateTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = TaskItem.Create(
            request.Title,
            ParseKind(request.Kind),
            request.Description);

        await repository.AddAsync(task, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // F7-T006: histórico com o usuário logado como actor.
        await historyWriter.RecordAsync(
            task.Id, TaskHistoryAction.Created, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Task {TaskId} created ({Kind} — {Title}).",
            task.Id, task.Kind, task.Title);

        return TaskDto.From(task);
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
