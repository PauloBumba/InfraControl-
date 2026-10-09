using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Articles.Commands;

// F24-T005/K5 (spec docs/32 §6 governança + §12 revisão): agenda de
// revisão periódica por artigo ("Revisar Knowledge a cada 180 dias") e
// a ação "manter" (reconfirma sem mudar conteúdo). As outras ações da
// §12 já existem: "atualizar" = PUT (reagenda sozinho) e "arquivar" =
// POST {id}/status (ADR-034); "substituir" = editar o conteúdo (PUT) —
// a substituição por OUTRO artigo é o vínculo de origem (K4).

public sealed record SetKnowledgeArticleReviewScheduleCommand(
    Guid Id,
    int? IntervalDays,
    Guid? ReviewResponsibleId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class SetKnowledgeArticleReviewScheduleCommandHandler(
    IKnowledgeArticleRepository repository,
    ILogger<SetKnowledgeArticleReviewScheduleCommandHandler> logger)
    : IRequestHandler<SetKnowledgeArticleReviewScheduleCommand>
{
    public async Task Handle(
        SetKnowledgeArticleReviewScheduleCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        if (request.ReviewResponsibleId is { } responsibleId)
            await CreateKnowledgeArticleCommandHandler
                .EnsureResponsibleExistsAsync(
                    repository, responsibleId, cancellationToken);

        // Domínio valida o intervalo (400 pt-BR) e recalcula a data.
        article.SetReviewSchedule(
            request.IntervalDays, request.ReviewResponsibleId);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} review schedule set ({IntervalDays}d, responsible {ResponsibleId}).",
            article.Id, request.IntervalDays, request.ReviewResponsibleId);
    }
}

public sealed record ConfirmKnowledgeArticleReviewCommand(
    Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

/// <summary>Ação "manter" da §12 — 409 se o artigo não tem intervalo.</summary>
public sealed class ConfirmKnowledgeArticleReviewCommandHandler(
    IKnowledgeArticleRepository repository,
    ILogger<ConfirmKnowledgeArticleReviewCommandHandler> logger)
    : IRequestHandler<ConfirmKnowledgeArticleReviewCommand>
{
    public async Task Handle(
        ConfirmKnowledgeArticleReviewCommand request,
        CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        article.ConfirmReview(); // InvalidOperationException → 409

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} review confirmed → {NextReviewAt:yyyy-MM-dd}.",
            article.Id, article.NextReviewAt);
    }
}
