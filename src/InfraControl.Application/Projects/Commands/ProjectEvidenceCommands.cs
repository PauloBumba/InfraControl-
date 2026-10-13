using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

// CRUD-Projetos (2026-10-12) — sub-agregado de evidências técnicas do Projeto
// (ADR-043 · docs/38 §3.4 · ADR-033/034): metadados no banco, binário em
// `var/project-evidence`. Antes só existia o repositório (leitura). Espelha o
// `PdiEvidenceCommands`: upload do autor (arquivo ≤ 10 MB, nome saneado),
// revisão única (validar / rejeitar com motivo) e remoção (arquivo + row).
// Arquivo é salvo ANTES do banco; se o insert falhar, remove o órfão
// (best-effort).

public sealed record UploadProjectEvidenceCommand(
    Guid ProjectId,
    ProjectEvidenceType Type,
    string? Description,
    string FileName,
    string? ContentType,
    long FileSize,
    Stream Content) : BaseCommand<ProjectEvidenceDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class UploadProjectEvidenceCommandHandler(
    IProjectRepository projectRepository,
    IProjectEvidenceRepository evidenceRepository,
    IProjectEvidenceFileStore fileStore,
    ICurrentUserContext currentUser,
    ILogger<UploadProjectEvidenceCommandHandler> logger)
    : IRequestHandler<UploadProjectEvidenceCommand, ProjectEvidenceDto>
{
    public async Task<ProjectEvidenceDto> Handle(
        UploadProjectEvidenceCommand request, CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Projeto '{request.ProjectId}' não encontrado.");

        var evidence = ProjectEvidence.Create(
            request.ProjectId,
            currentUser.UserId!.Value,
            request.Type,
            request.Description,
            request.FileName,
            request.ContentType,
            request.FileSize);

        await fileStore.SaveAsync(
            evidence.Id, evidence.Extension, request.Content, cancellationToken);
        try
        {
            await evidenceRepository.AddAsync(evidence, cancellationToken);
            await evidenceRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Banco falhou → remove o arquivo órfão (best-effort).
            try
            {
                await fileStore.DeleteAsync(
                    evidence.Id, evidence.Extension, CancellationToken.None);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }

        logger.LogInformation(
            "Project evidence {Id} uploaded for project {ProjectId} ({Type}).",
            evidence.Id, evidence.ProjectId, evidence.Type);

        return ToDto(evidence);
    }

    internal static ProjectEvidenceDto ToDto(ProjectEvidence evidence) =>
        new(
            evidence.Id,
            evidence.ProjectId,
            evidence.Type.ToString(),
            evidence.Status.ToString(),
            evidence.Description,
            evidence.OriginalFileName,
            evidence.ContentType,
            evidence.FileSize,
            evidence.CreatedBy,
            evidence.CreateDateTime,
            evidence.ReviewedBy,
            evidence.ReviewedAt,
            evidence.RejectionReason);
}

public sealed record ReviewProjectEvidenceCommand(
    Guid Id,
    ProjectEvidenceReviewDecision Decision,
    string? RejectionReason = null) : BaseCommand<ProjectEvidenceDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class ReviewProjectEvidenceCommandHandler(
    IProjectEvidenceRepository evidenceRepository,
    ICurrentUserContext currentUser,
    ILogger<ReviewProjectEvidenceCommandHandler> logger)
    : IRequestHandler<ReviewProjectEvidenceCommand, ProjectEvidenceDto>
{
    public async Task<ProjectEvidenceDto> Handle(
        ReviewProjectEvidenceCommand request, CancellationToken cancellationToken)
    {
        var evidence = await evidenceRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        var reviewerId = currentUser.UserId!.Value;
        switch (request.Decision)
        {
            case ProjectEvidenceReviewDecision.Validated:
                evidence.ValidateBy(reviewerId);
                break;
            case ProjectEvidenceReviewDecision.Rejected:
                evidence.RejectBy(reviewerId, request.RejectionReason);
                break;
            default:
                throw new ArgumentException(
                    "A decisão de revisão é inválida.", nameof(request.Decision));
        }

        await evidenceRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Project evidence {Id} {Decision} by {Reviewer}.",
            evidence.Id,
            request.Decision == ProjectEvidenceReviewDecision.Validated
                ? "validated" : "rejected",
            reviewerId);

        return UploadProjectEvidenceCommandHandler.ToDto(evidence);
    }
}

public sealed record DeleteProjectEvidenceCommand(Guid Id)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "Project";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class DeleteProjectEvidenceCommandHandler(
    IProjectEvidenceRepository evidenceRepository,
    IProjectEvidenceFileStore fileStore,
    ILogger<DeleteProjectEvidenceCommandHandler> logger)
    : IRequestHandler<DeleteProjectEvidenceCommand>
{
    public async Task Handle(
        DeleteProjectEvidenceCommand request, CancellationToken cancellationToken)
    {
        var evidence = await evidenceRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        evidenceRepository.Remove(evidence);
        await evidenceRepository.SaveChangesAsync(cancellationToken);

        // Row removida → remove o binário (best-effort; arquivo órfão não
        // quebra nada, só ocupa espaço).
        try
        {
            await fileStore.DeleteAsync(
                evidence.Id, evidence.Extension, CancellationToken.None);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        logger.LogInformation(
            "Project evidence {Id} deleted.", evidence.Id);
    }
}
