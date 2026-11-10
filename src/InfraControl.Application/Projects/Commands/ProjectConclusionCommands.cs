using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

/// <summary>DTO de leitura de uma solicitação de conclusão de Projeto.</summary>
public sealed record ProjectConclusionDto(
    Guid Id,
    Guid ProjectId,
    string Status,
    string Description,
    string ActivitiesPerformed,
    string ResultsAchieved,
    string? ProblemsEncountered,
    string? SolutionsImplemented,
    string? EvidenceSummary,
    DateTimeOffset CompletedAt,
    Guid SubmittedById,
    string? SubmittedByName,
    Guid? DecidedById,
    string? DecidedByName,
    string? DecisionNotes,
    DateTimeOffset? DecidedAt,
    DateTime CreateDateTime)
{
    public static ProjectConclusionDto From(
        ProjectConclusion conclusion,
        IReadOnlyDictionary<Guid, string>? names = null) => new(
        conclusion.Id,
        conclusion.ProjectId,
        conclusion.Status.ToString(),
        conclusion.Description,
        conclusion.ActivitiesPerformed,
        conclusion.ResultsAchieved,
        conclusion.ProblemsEncountered,
        conclusion.SolutionsImplemented,
        conclusion.EvidenceSummary,
        conclusion.CompletedAt,
        conclusion.SubmittedById,
        Resolve(names, conclusion.SubmittedById),
        conclusion.DecidedById,
        Resolve(names, conclusion.DecidedById),
        conclusion.DecisionNotes,
        conclusion.DecidedAt,
        conclusion.CreateDateTime);

    private static string? Resolve(
        IReadOnlyDictionary<Guid, string>? names, Guid? id) =>
        names is not null && id.HasValue && names.TryGetValue(id.Value, out var name)
            ? name
            : null;
}

/// <summary>
/// Solicitação formal de conclusão (spec §6): o responsável descreve o
/// trabalho realizado; o projeto avança para PendingFinalApproval.
/// </summary>
public sealed record SubmitProjectConclusionCommand(
    Guid ProjectId,
    string? Description = null,
    string? ActivitiesPerformed = null,
    string? ResultsAchieved = null,
    string? ProblemsEncountered = null,
    string? SolutionsImplemented = null,
    string? EvidenceSummary = null,
    DateTimeOffset? CompletedAt = null) : BaseCommand<ProjectConclusionDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SubmitProjectConclusionCommandHandler(
    IProjectRepository projectRepository,
    IProjectConclusionRepository conclusionRepository,
    IProjectHistoryRepository historyRepository,
    ICurrentUserContext currentUser,
    ILogger<SubmitProjectConclusionCommandHandler> logger)
    : IRequestHandler<SubmitProjectConclusionCommand, ProjectConclusionDto>
{
    public async Task<ProjectConclusionDto> Handle(
        SubmitProjectConclusionCommand request, CancellationToken cancellationToken)
    {
        var project = ProjectAccess.EnsureVisible(
            await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken),
            currentUser,
            request.ProjectId);

        if (currentUser.UserId is not { } submittedById)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado.");

        if (project.Status != ProjectStatus.InProgress)
            throw new InvalidOperationException(
                "A solicitação de conclusão só pode ser feita por projetos em andamento.");

        if (await conclusionRepository.GetPendingByProjectIdAsync(project.Id, cancellationToken) is not null)
            throw new InvalidOperationException(
                "Já existe uma solicitação de conclusão pendente para este projeto.");

        var conclusion = ProjectConclusion.Submit(
            project.Id,
            request.Description!,
            request.ActivitiesPerformed!,
            request.ResultsAchieved!,
            request.ProblemsEncountered,
            request.SolutionsImplemented,
            request.EvidenceSummary,
            request.CompletedAt ?? DateTimeOffset.UtcNow,
            submittedById);

        await conclusionRepository.AddAsync(conclusion, cancellationToken);

        project.SubmitForApproval();

        await historyRepository.AddAsync(
            ProjectHistoryEntry.Create(
                project.Id,
                ProjectHistoryType.ConclusionSubmitted,
                submittedById,
                $"Conclusão '{conclusion.Id}' submetida para análise administrativa."),
            cancellationToken);

        await conclusionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProjectConclusion {ConclusionId} submitted for project {ProjectId}.",
            conclusion.Id, project.Id);

        var names = await projectRepository.GetUserNamesAsync(
            [submittedById], cancellationToken);

        return ProjectConclusionDto.From(conclusion, names);
    }
}

/// <summary>Lista as solicitações de conclusão do projeto (mais recentes primeiro).</summary>
public sealed record GetProjectConclusionsQuery(Guid ProjectId)
    : BaseQuery<ProjectConclusionDto[]>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetProjectConclusionsQueryHandler(
    IProjectRepository projectRepository,
    IProjectConclusionRepository conclusionRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetProjectConclusionsQuery, ProjectConclusionDto[]>
{
    public async Task<ProjectConclusionDto[]> Handle(
        GetProjectConclusionsQuery request, CancellationToken cancellationToken)
    {
        _ = ProjectAccess.EnsureVisible(
            await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken),
            currentUser,
            request.ProjectId);

        var conclusions = await conclusionRepository.ListByProjectIdAsync(
            request.ProjectId, cancellationToken);

        var names = await projectRepository.GetUserNamesAsync(
            conclusions
                .SelectMany(c => new[] { c.SubmittedById, c.DecidedById ?? Guid.Empty })
                .Where(id => id != Guid.Empty)
                .ToArray(),
            cancellationToken);

        return conclusions
            .Select(c => ProjectConclusionDto.From(c, names))
            .ToArray();
    }
}

/// <summary>
/// Aprovação administrativa da conclusão (spec §6): conclui o projeto e
/// gera o registro na base de conhecimento de forma atômica — a geração
/// interna grava artigo+versão+origem no mesmo SaveChanges do contexto.
/// </summary>
public sealed record ApproveProjectConclusionCommand(
    Guid ProjectId,
    string? ApprovalNotes = null) : BaseCommand<ProjectDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class ApproveProjectConclusionCommandHandler(
    IProjectRepository projectRepository,
    IProjectConclusionRepository conclusionRepository,
    IProjectHistoryRepository historyRepository,
    ICurrentUserContext currentUser,
    ISender sender,
    ILogger<ApproveProjectConclusionCommandHandler> logger)
    : IRequestHandler<ApproveProjectConclusionCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        ApproveProjectConclusionCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.ProjectId}' não encontrado.");

        var conclusion = await conclusionRepository.GetPendingByProjectIdAsync(project.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Não existe solicitação de conclusão pendente para este projeto.");

        if (currentUser.UserId is not { } adminId)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado para aprovação.");

        var now = DateTimeOffset.UtcNow;
        conclusion.Approve(adminId, request.ApprovalNotes, now);
        project.Approve(adminId, request.ApprovalNotes, now);

        await historyRepository.AddAsync(
            ProjectHistoryEntry.Create(
                project.Id,
                ProjectHistoryType.ConclusionApproved,
                adminId,
                $"Conclusão '{conclusion.Id}' aprovada.",
                request.ApprovalNotes),
            cancellationToken);

        await historyRepository.AddAsync(
            ProjectHistoryEntry.Create(
                project.Id,
                ProjectHistoryType.Approved,
                adminId,
                null,
                request.ApprovalNotes),
            cancellationToken);

        // Evento de negócio: aprovação → conhecimento. O comando interno
        // grava artigo+versão+origem no mesmo DbContext — commit atômico.
        // Idempotente: se já existe origem (Project, projectId), retorna o
        // artigo existente em vez de duplicar.
        await sender.Send(
            new GenerateProjectKnowledgeCommand(project.Id),
            cancellationToken);

        await projectRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProjectConclusion {ConclusionId} approved; project {ProjectId} completed by {AdminId}.",
            conclusion.Id, project.Id, adminId);

        var names = await projectRepository.GetUserNamesAsync(
            [project.LeaderId, project.CreatedById ?? Guid.Empty, adminId],
            cancellationToken);

        return ProjectDto.From(project, names);
    }
}

/// <summary>
/// Devolução da conclusão para ajustes (spec §6): reprovação com
/// justificativa ou solicitação de ajustes — o projeto volta a InProgress.
/// </summary>
public sealed record ReturnProjectConclusionCommand(
    Guid ProjectId,
    string? Decision = null,
    string? Justification = null) : BaseCommand<ProjectDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class ReturnProjectConclusionCommandHandler(
    IProjectRepository projectRepository,
    IProjectConclusionRepository conclusionRepository,
    IProjectHistoryRepository historyRepository,
    ICurrentUserContext currentUser,
    ILogger<ReturnProjectConclusionCommandHandler> logger)
    : IRequestHandler<ReturnProjectConclusionCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(
        ReturnProjectConclusionCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.ProjectId}' não encontrado.");

        var conclusion = await conclusionRepository.GetPendingByProjectIdAsync(project.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Não existe solicitação de conclusão pendente para este projeto.");

        if (currentUser.UserId is not { } adminId)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado para decisão.");

        var requestChanges = string.Equals(
            request.Decision, "requestchanges", StringComparison.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        if (requestChanges)
            conclusion.RequestChanges(adminId, request.Justification!, now);
        else
            conclusion.Reject(adminId, request.Justification!, now);

        // PendingFinalApproval → InProgress é transição válida do agregado.
        if (project.Status == ProjectStatus.PendingFinalApproval)
            project.TransitionTo(ProjectStatus.InProgress, request.Justification);

        await historyRepository.AddAsync(
            ProjectHistoryEntry.Create(
                project.Id,
                requestChanges
                    ? ProjectHistoryType.ConclusionChangesRequested
                    : ProjectHistoryType.ConclusionRejected,
                adminId,
                requestChanges
                    ? $"Conclusão '{conclusion.Id}' devolvida para ajustes."
                    : $"Conclusão '{conclusion.Id}' reprovada.",
                request.Justification),
            cancellationToken);

        await conclusionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProjectConclusion {ConclusionId} returned ({Decision}) for project {ProjectId}.",
            conclusion.Id, requestChanges ? "changes" : "rejected", project.Id);

        return ProjectDto.From(project);
    }
}
