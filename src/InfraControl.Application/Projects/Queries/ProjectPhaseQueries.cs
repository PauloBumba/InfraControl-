using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Projects.Queries;

/// <summary>
/// Lista as fases do projeto em ordem sequencial e o progresso global ponderado.
/// </summary>
public sealed record GetProjectPhasesQuery(Guid ProjectId)
    : BaseQuery<ProjectPhasesSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetProjectPhasesQueryHandler(
    IProjectRepository projectRepository,
    IProjectPhaseRepository phaseRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetProjectPhasesQuery, ProjectPhasesSummaryDto>
{
    public async Task<ProjectPhasesSummaryDto> Handle(
        GetProjectPhasesQuery request, CancellationToken cancellationToken)
    {
        _ = ProjectAccess.EnsureVisible(
            await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken),
            currentUser,
            request.ProjectId);

        var phases = await phaseRepository.GetByProjectIdAsync(
            request.ProjectId, cancellationToken);

        return new ProjectPhasesSummaryDto(
            request.ProjectId,
            ProjectProgressCalculator.ComputeOverallPercent(phases),
            phases.Select(AddProjectPhaseCommandHandler.ToDto).ToArray());
    }
}
