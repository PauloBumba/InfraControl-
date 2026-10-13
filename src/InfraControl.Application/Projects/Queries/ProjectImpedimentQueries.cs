using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Projects.Queries;

// CRUD-Projetos (2026-10-12) — lista de impedimentos do projeto (read-model
// para a UI; a escrita vem dos commands de ProjectImpedimentCommands).

public sealed record GetProjectImpedimentsQuery(Guid ProjectId)
    : IQuery<ProjectImpedimentDto[]>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetProjectImpedimentsQueryHandler(
    IProjectImpedimentRepository impedimentRepository)
    : IRequestHandler<GetProjectImpedimentsQuery, ProjectImpedimentDto[]>
{
    public async Task<ProjectImpedimentDto[]> Handle(
        GetProjectImpedimentsQuery request, CancellationToken cancellationToken)
    {
        var impediments = await impedimentRepository.ListByProjectIdAsync(
            request.ProjectId, cancellationToken);

        return impediments
            .Select(CreateProjectImpedimentCommandHandler.ToDto)
            .ToArray();
    }
}
