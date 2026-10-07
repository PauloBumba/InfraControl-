using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// Evidências do PDI (docs/30 §12, §17, Fase 4 — ADR-034): upload do
// colaborador (9 tipos fixos, arquivo ≤ 10 MB, nome saneado), revisão
// única do gestor/Admin (validada / rejeitada com motivo) e registro no
// timeline (EvidenceAdded/EvidenceReviewed). Arquivo salvo antes do
// banco (rollback best-effort se o save falhar).

public sealed record UploadPdiEvidenceCommand(
    Guid PhaseId,
    PdiEvidenceType Type,
    string? Description,
    string FileName,
    string? ContentType,
    long FileSize,
    Stream Content) : BaseCommand<PdiEvidenceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UploadPdiEvidenceCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    IPdiEvidenceFileStore fileStore,
    ILogger<UploadPdiEvidenceCommandHandler> logger)
    : IRequestHandler<UploadPdiEvidenceCommand, PdiEvidenceDto>
{
    public async Task<PdiEvidenceDto> Handle(
        UploadPdiEvidenceCommand request, CancellationToken cancellationToken)
    {
        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, request.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReport(currentUser, plan);

        var evidence = PdiEvidence.Create(
            request.PhaseId, currentUser.UserId!.Value, request.Type,
            request.Description, request.FileName, request.ContentType,
            request.FileSize);

        await fileStore.SaveAsync(
            evidence.Id, evidence.Extension, request.Content, cancellationToken);
        try
        {
            repository.AddEvidence(evidence);
            plan.RecordEvidenceAdded(
                currentUser.UserId!.Value, request.Type, request.Description);
            plans.AddHistoryRange(plan.DrainPendingHistory());
            await repository.SaveChangesAsync(cancellationToken);
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
            "PDI evidence {Id} uploaded for phase {PhaseId} ({Type}).",
            evidence.Id, evidence.PhaseId, evidence.Type);

        var authorName = await plans.GetUserNameAsync(
            evidence.CreatedBy, cancellationToken);
        return new PdiEvidenceDto(
            evidence.Id, evidence.PhaseId, evidence.Type.ToString(),
            evidence.Status.ToString(), evidence.Description,
            evidence.OriginalFileName, evidence.ContentType, evidence.FileSize,
            evidence.CreatedBy, authorName, evidence.CreateDateTime,
            ReviewedBy: null, ReviewedByName: null, ReviewedAt: null,
            RejectionReason: null);
    }
}

public sealed record ReviewPdiEvidenceCommand(
    Guid Id,
    PdiEvidenceReviewDecision Decision,
    string? RejectionReason) : BaseCommand<PdiEvidenceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ReviewPdiEvidenceCommandHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    ILogger<ReviewPdiEvidenceCommandHandler> logger)
    : IRequestHandler<ReviewPdiEvidenceCommand, PdiEvidenceDto>
{
    public async Task<PdiEvidenceDto> Handle(
        ReviewPdiEvidenceCommand request, CancellationToken cancellationToken)
    {
        var evidence = await repository.GetEvidenceAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, evidence.PhaseId, cancellationToken);
        PdiAccess.EnsureCanReview(currentUser, plan);

        var reviewerId = currentUser.UserId!.Value;
        var approved = request.Decision switch
        {
            PdiEvidenceReviewDecision.Validated => true,
            PdiEvidenceReviewDecision.Rejected => false,
            _ => throw new ArgumentException(
                "A decisão de revisão é inválida.", nameof(request.Decision))
        };

        if (approved)
            evidence.ValidateBy(reviewerId);
        else
            evidence.RejectBy(reviewerId, request.RejectionReason);

        plan.RecordEvidenceReviewed(
            reviewerId, approved,
            approved ? null : request.RejectionReason);
        plans.AddHistoryRange(plan.DrainPendingHistory());
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI evidence {Id} {Decision} by {Reviewer}.",
            evidence.Id,
            approved ? "validated" : "rejected",
            reviewerId);

        var reviewerName = await plans.GetUserNameAsync(
            reviewerId, cancellationToken);
        return new PdiEvidenceDto(
            evidence.Id, evidence.PhaseId, evidence.Type.ToString(),
            evidence.Status.ToString(), evidence.Description,
            evidence.OriginalFileName, evidence.ContentType, evidence.FileSize,
            evidence.CreatedBy,
            await plans.GetUserNameAsync(evidence.CreatedBy, cancellationToken),
            evidence.CreateDateTime,
            evidence.ReviewedBy, reviewerName, evidence.ReviewedAt,
            evidence.RejectionReason);
    }
}
