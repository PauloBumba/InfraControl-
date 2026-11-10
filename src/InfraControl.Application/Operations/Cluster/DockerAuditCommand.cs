using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;

namespace InfraControl.Application.Operations.Cluster;

/// <summary>
/// Trilha de auditoria das ações Docker privilegiadas (start/stop/restart).
/// O AuditBehavior registra no pipeline; o handler existe só para satisfazer
/// o MediatR — a "persistência" é a própria AuditEntry gerada pelo behavior.
/// </summary>
public sealed record RecordDockerActionAuditCommand(string Container, string Action)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => Domain.Enums.RoleAccess.Administer;
    public string AuditResource => "DockerContainer";
}

public sealed class RecordDockerActionAuditCommandHandler
    : IRequestHandler<RecordDockerActionAuditCommand>
{
    public Task Handle(
        RecordDockerActionAuditCommand request,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
