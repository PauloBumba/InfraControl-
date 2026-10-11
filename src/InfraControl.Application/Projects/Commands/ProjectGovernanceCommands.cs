using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

/// <summary>
/// Submete um projeto para homologação formal pelo Administrador.
/// </summary>
public sealed record SubmitProjectForApprovalCommand(Guid ProjectId)
    : BaseCommand<ProjectDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SubmitProjectForApprovalCommandHandler(
    IProjectRepository projectRepository,
    ILogger<SubmitProjectForApprovalCommandHandler> logger)
    : IRequestHandler<SubmitProjectForApprovalCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        SubmitProjectForApprovalCommand request,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.ProjectId}' não encontrado.");

        project.SubmitForApproval();
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project {ProjectId} submitted for final approval.",
            request.ProjectId);

        return ProjectDto.From(project);
    }
}

/// <summary>
/// Homologa o encerramento do projeto por um administrador.
/// </summary>
public sealed record ApproveProjectCommand(Guid ProjectId, string? ApprovalNotes = null)
    : BaseCommand<ProjectDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class ApproveProjectCommandHandler(
    IProjectRepository projectRepository,
    ICurrentUserContext currentUser,
    ILogger<ApproveProjectCommandHandler> logger)
    : IRequestHandler<ApproveProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        ApproveProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.ProjectId}' não encontrado.");

        if (!currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado para aprovação.");

        project.Approve(currentUser.UserId.Value, request.ApprovalNotes, DateTimeOffset.UtcNow);
        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project {ProjectId} approved by admin {AdminId}.",
            request.ProjectId, currentUser.UserId.Value);

        return ProjectDto.From(project);
    }
}
