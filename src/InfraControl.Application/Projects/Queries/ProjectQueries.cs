using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Projects.Queries;

public sealed record SearchProjectsQuery(
    string? Term = null,
    string? Status = null,
    string? Priority = null,
    Guid? LeaderId = null,
    int Page = 1,
    int PageSize = 20) : BaseQuery<PagedResult<ProjectDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class SearchProjectsQueryHandler(
    IProjectRepository projectRepository)
    : IRequestHandler<SearchProjectsQuery, PagedResult<ProjectDto>>
{
    public async Task<PagedResult<ProjectDto>> Handle(
        SearchProjectsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var status = ParseStatus(request.Status);
        var priority = ParsePriority(request.Priority);

        var result = await projectRepository.SearchAsync(
            string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim(),
            status,
            priority,
            request.LeaderId,
            page,
            pageSize,
            cancellationToken);

        return new PagedResult<ProjectDto>(
            result.Items.Select(ProjectDto.From).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    internal static ProjectStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        if (!Enum.TryParse(status.Trim(), ignoreCase: true, out ProjectStatus parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Status inválido: '{status}'. Use draft, planned, inprogress, pendingfinalapproval, completed, cancelled ou onhold.",
                nameof(status));

        return parsed;
    }

    internal static ProjectPriority? ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return null;

        if (!Enum.TryParse(priority.Trim(), ignoreCase: true, out ProjectPriority parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Prioridade inválida: '{priority}'. Use low, medium, high ou critical.",
                nameof(priority));

        return parsed;
    }
}

public sealed record GetProjectQuery(Guid Id)
    : BaseQuery<ProjectDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetProjectQueryHandler(
    IProjectRepository projectRepository)
    : IRequestHandler<GetProjectQuery, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.Id}' não encontrado.");

        return ProjectDto.From(project);
    }
}
