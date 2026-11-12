using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Cluster;

/// <summary>
/// Tela de status (F50): listagem de containers em nível de leitura —
/// qualquer role autenticada vê o parque; escala/remoção continua só
/// no ClusterController (Administer).
/// </summary>
public sealed record GetDockerStatusQuery
    : IQuery<DockerStatusDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed record DockerStatusDto(
    bool Available,
    IReadOnlyList<DockerContainerDto> Containers);

public sealed class GetDockerStatusQueryHandler(IDockerControl docker)
    : IRequestHandler<GetDockerStatusQuery, DockerStatusDto>
{
    public async Task<DockerStatusDto> Handle(
        GetDockerStatusQuery request, CancellationToken cancellationToken)
    {
        // Null = Docker indisponível (socket ausente em produção) — a
        // página de status degrada para "indisponível" sem quebrar.
        var containers = await docker.ListContainersAsync(cancellationToken);
        return containers is null
            ? new DockerStatusDto(false, [])
            : new DockerStatusDto(true, containers);
    }
}
