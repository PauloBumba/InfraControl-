using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T007 (Workflow): transição de estado da Task — o mapa fica no domínio
// (TaskStateWorkflow); aqui só 404 (task), 400 (estado desconhecido/vazio)
// e a propagação da InvalidOperationException do domínio → 409.

public sealed record ChangeTaskStateCommand(Guid Id, string State)
    : BaseCommand<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksManage.ToString()];
}

public sealed class ChangeTaskStateCommandHandler(
    ITaskItemRepository repository,
    ITaskHistoryWriter historyWriter,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository,
    ICurrentUserContext currentUser,
    ILogger<ChangeTaskStateCommandHandler> logger)
    : IRequestHandler<ChangeTaskStateCommand, TaskDto>
{
    public async Task<TaskDto> Handle(
        ChangeTaskStateCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        var target = SearchTasksQueryHandler.ParseState(request.State)
            ?? throw new ArgumentException(
                "Estado é obrigatório. Use open, inprogress, waiting, blocked, "
                + "resolved, cancelled ou closed.",
                nameof(request.State));

        var previous = task.State;
        task.ChangeState(target); // domínio: 409 se fora do mapa (ou no-op se igual)

        if (previous == task.State)
            return await BuildDtoAsync(task, cancellationToken); // mesmo estado: sem histórico

        await repository.SaveChangesAsync(cancellationToken);

        // F7-T006: histórico com o estado anterior → novo.
        await historyWriter.RecordAsync(
            task.Id,
            TaskHistoryAction.StateChanged,
            $"{previous} → {task.State}",
            cancellationToken);

        logger.LogInformation(
            "Task {TaskId} state: {From} → {To} (user {User}).",
            task.Id, previous, task.State, currentUser.UserId);

        return await BuildDtoAsync(task, cancellationToken);
    }

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

        return TaskDto.From(
            task,
            assignees.TryGetValue(task.Id, out var a) ? a : null,
            devices.TryGetValue(task.Id, out var d) ? d : null,
            incidents.TryGetValue(task.Id, out var i) ? i : null);
    }
}
