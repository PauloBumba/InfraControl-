using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

// CRUD-Projetos (2026-10-12) — gestão da equipe do Projeto (ADR-043 ·
// docs/38 §3.3). AddMember do agregado faz upsert de papel (usuário já
// existente → UpdateRole), então "adicionar" e "trocar papel" são o mesmo
// command; remover é explícito. Antes: só leitura (Members exposto no DTO).

public sealed record AddProjectMemberCommand(
    Guid ProjectId,
    Guid UserId,
    string Role) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class AddProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    ILogger<AddProjectMemberCommandHandler> logger)
    : IRequestHandler<AddProjectMemberCommand>
{
    public async Task Handle(
        AddProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        project.AddMember(request.UserId, ParseRole(request.Role));
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} added to project {ProjectId} as {Role}.",
            request.UserId, request.ProjectId, request.Role);
    }

    internal static ProjectMemberRole ParseRole(string role)
    {
        if (!Enum.TryParse<ProjectMemberRole>(role, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Papel de membro inválido (use Leader, Member ou Reviewer).",
                nameof(role));
        return parsed;
    }
}

public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class RemoveProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    ILogger<RemoveProjectMemberCommandHandler> logger)
    : IRequestHandler<RemoveProjectMemberCommand>
{
    public async Task Handle(
        RemoveProjectMemberCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        project.RemoveMember(request.UserId);
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} removed from project {ProjectId}.",
            request.UserId, request.ProjectId);
    }
}
