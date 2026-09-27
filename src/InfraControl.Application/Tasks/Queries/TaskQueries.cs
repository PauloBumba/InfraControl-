using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F7-T001: leitura da Task — filtros termo/tipo/estado (docs/04);
// entrada inválida → 400 pt-BR; id inexistente → 404 pt-BR.

public sealed record SearchTasksQuery(
    string? Term = null,
    string? Kind = null,
    string? State = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<TaskDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksRead.ToString()];
}

public sealed class SearchTasksQueryHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository)
    : IRequestHandler<SearchTasksQuery, PagedResult<TaskDto>>
{
    public async Task<PagedResult<TaskDto>> Handle(
        SearchTasksQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var kind = ParseKind(request.Kind);
        var state = ParseState(request.State);

        var result = await repository.SearchAsync(
            string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim(),
            kind, state, page, pageSize, cancellationToken);

        // F7-T002: group join de assignees da página inteira (sem N+1).
        var assignees = await assigneeRepository.GetUserIdsByTaskIdsAsync(
            result.Items.Select(task => task.Id).ToArray(), cancellationToken);

        return new PagedResult<TaskDto>(
            result.Items.Select(task => TaskDto.From(
                task,
                assignees.TryGetValue(task.Id, out var ids) ? ids : null))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    internal static TaskKind? ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return null;

        if (!Enum.TryParse(kind.Trim(), ignoreCase: true, out TaskKind parsed))
            throw new ArgumentException(
                $"Tipo inválido: '{kind}'. Use manual, incident, alert, monitoring, "
                + "discovery, maintenance, change, preventive, corrective, project ou system.",
                nameof(kind));

        return parsed;
    }

    internal static TaskState? ParseState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;

        if (!Enum.TryParse(state.Trim(), ignoreCase: true, out TaskState parsed))
            throw new ArgumentException(
                $"Estado inválido: '{state}'. Use open, inprogress, waiting, blocked, "
                + "resolved, cancelled ou closed.",
                nameof(state));

        return parsed;
    }
}

public sealed record GetTaskQuery(Guid Id)
    : IQuery<TaskDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksRead.ToString()];
}

public sealed class GetTaskQueryHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository)
    : IRequestHandler<GetTaskQuery, TaskDto>
{
    public async Task<TaskDto> Handle(
        GetTaskQuery request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        var assigneeIds = await assigneeRepository.GetUserIdsAsync(
            task.Id, cancellationToken);

        return TaskDto.From(task, assigneeIds);
    }
}
