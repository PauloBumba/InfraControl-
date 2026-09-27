using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T002: atribuição/desatribuição de responsável — task/usuário
// inexistente → 404 pt-BR; duplicidade → 409 pt-BR.

public sealed record AssignTaskCommand(
    Guid TaskId,
    Guid UserId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksManage.ToString()];
}

public sealed class AssignTaskCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskAssigneeRepository assigneeRepository,
    ILogger<AssignTaskCommandHandler> logger)
    : IRequestHandler<AssignTaskCommand>
{
    public async Task Handle(
        AssignTaskCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        if (!await assigneeRepository.UserExistsAsync(request.UserId, cancellationToken))
            throw new KeyNotFoundException(
                $"Usuário '{request.UserId}' não encontrado.");

        if (await assigneeRepository.ExistsAsync(
                request.TaskId, request.UserId, cancellationToken))
            throw new InvalidOperationException(
                "O usuário já está atribuído à task.");

        await assigneeRepository.AddAsync(
            TaskAssignee.Create(request.TaskId, request.UserId), cancellationToken);
        await assigneeRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Task {TaskId} assigned to user {UserId}.",
            request.TaskId, request.UserId);
    }
}

public sealed record UnassignTaskCommand(
    Guid TaskId,
    Guid UserId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksManage.ToString()];
}

public sealed class UnassignTaskCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskAssigneeRepository assigneeRepository,
    ILogger<UnassignTaskCommandHandler> logger)
    : IRequestHandler<UnassignTaskCommand>
{
    public async Task Handle(
        UnassignTaskCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var assignee = await assigneeRepository.FindAsync(
                request.TaskId, request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo da task '{request.TaskId}' com o usuário "
                + $"'{request.UserId}' não encontrado.");

        assigneeRepository.Remove(assignee);
        await assigneeRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Task {TaskId} unassigned from user {UserId}.",
            request.TaskId, request.UserId);
    }
}
