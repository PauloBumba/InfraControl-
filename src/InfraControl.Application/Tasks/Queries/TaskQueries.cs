using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
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
    int PageSize = 20,
    Guid? ParentId = null,
    bool RootOnly = false) : IQuery<PagedResult<TaskDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchTasksQueryHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository)
    : IRequestHandler<SearchTasksQuery, PagedResult<TaskDto>>
{
    public async Task<PagedResult<TaskDto>> Handle(
        SearchTasksQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var kind = ParseKind(request.Kind);
        var state = ParseState(request.State);

        // F23-T004/P4a: filtros de hierarquia mutuamente exclusivos.
        if (request.RootOnly && request.ParentId != null)
            throw new ArgumentException(
                "Os filtros rootOnly e parentId são incompatíveis.",
                nameof(request));

        var result = await repository.SearchAsync(
            string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim(),
            kind, state, page, pageSize,
            request.ParentId, request.RootOnly, cancellationToken);

        // F7-T002/T003: group joins da página inteira (sem N+1).
        var pageIds = result.Items.Select(task => task.Id).ToArray();
        var assignees = await assigneeRepository.GetUserIdsByTaskIdsAsync(
            pageIds, cancellationToken);
        var devices = await deviceRepository.GetDeviceIdsByTaskIdsAsync(
            pageIds, cancellationToken);
        var incidents = await incidentRepository.GetIncidentIdsByTaskIdsAsync(
            pageIds, cancellationToken);

        // F23-T001: nomes dos responsáveis da página em 1 consulta.
        var responsibleIds = result.Items
            .Where(task => task.ResponsibleId != null)
            .Select(task => task.ResponsibleId!.Value)
            .Distinct()
            .ToArray();
        var responsibleNames = responsibleIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await assigneeRepository.GetUserNamesAsync(responsibleIds, cancellationToken);

        // F23-T004/P4a: progresso das subtarefas + títulos dos pais
        // (só as tasks filhas da página) — 2 consultas, sem N+1.
        var subtaskStats = await repository.GetSubtaskStatsAsync(
            pageIds, cancellationToken);
        var parentIds = result.Items
            .Where(task => task.ParentTaskId != null)
            .Select(task => task.ParentTaskId!.Value)
            .Distinct()
            .ToArray();
        var parentTitles = parentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await repository.GetTitlesAsync(parentIds, cancellationToken);

        return new PagedResult<TaskDto>(
            result.Items.Select(task => TaskDto.From(
                task,
                assignees.TryGetValue(task.Id, out var ids) ? ids : null,
                devices.TryGetValue(task.Id, out var deviceIds) ? deviceIds : null,
                incidents.TryGetValue(task.Id, out var incidentIds) ? incidentIds : null,
                task.ResponsibleId is { } rid
                    && responsibleNames.TryGetValue(rid, out var name) ? name : null,
                subtaskStats.TryGetValue(task.Id, out var stats) ? stats : null,
                task.ParentTaskId is { } pid
                    && parentTitles.TryGetValue(pid, out var parentTitle) ? parentTitle : null))
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
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskQueryHandler(
    ITaskItemRepository repository,
    ITaskAssigneeRepository assigneeRepository,
    ITaskDeviceRepository deviceRepository,
    ITaskIncidentRepository incidentRepository)
    : IRequestHandler<GetTaskQuery, TaskDto>
{
    public async Task<TaskDto> Handle(
        GetTaskQuery request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.Id}' não encontrada.");

        var assigneeIds = await assigneeRepository.GetUserIdsAsync(
            task.Id, cancellationToken);
        var deviceIds = await deviceRepository.GetDeviceIdsAsync(
            task.Id, cancellationToken);
        var incidentIds = await incidentRepository.GetIncidentIdsAsync(
            task.Id, cancellationToken);
        var responsibleName = await CreateTaskCommandHandler
            .ResolveResponsibleNameAsync(
                assigneeRepository, task.ResponsibleId, cancellationToken);

        // F23-T004/P4a: progresso das subtarefas + título do pai.
        var subtaskStats = await repository.GetSubtaskStatsAsync(
            [task.Id], cancellationToken);
        string? parentTitle = null;
        if (task.ParentTaskId is { } parentId)
        {
            var titles = await repository.GetTitlesAsync([parentId], cancellationToken);
            titles.TryGetValue(parentId, out parentTitle);
        }

        return TaskDto.From(
            task, assigneeIds, deviceIds, incidentIds, responsibleName,
            subtaskStats.TryGetValue(task.Id, out var stats) ? stats : null,
            parentTitle);
    }
}
