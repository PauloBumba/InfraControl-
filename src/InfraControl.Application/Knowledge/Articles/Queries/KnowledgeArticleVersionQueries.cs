using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Knowledge.Articles.Queries;

// F24-T003/K3 (spec docs/32 §8): histórico de versões nunca perdidas.
// Mesma visibilidade do artigo (ADR-034 §4): estado nunca-publicado
// sem escrita = 404, não vaza existência.

public sealed record ListKnowledgeArticleVersionsQuery(Guid ArticleId)
    : IQuery<IReadOnlyCollection<KnowledgeArticleVersionDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class ListKnowledgeArticleVersionsQueryHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeArticleVersionRepository versionRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<ListKnowledgeArticleVersionsQuery,
        IReadOnlyCollection<KnowledgeArticleVersionDto>>
{
    public async Task<IReadOnlyCollection<KnowledgeArticleVersionDto>> Handle(
        ListKnowledgeArticleVersionsQuery request, CancellationToken cancellationToken)
    {
        var article = await LoadArticleAsync(
            repository, currentUser, request.ArticleId, cancellationToken);

        var rows = await versionRepository.ListByArticleAsync(
            article.Id, cancellationToken);

        return rows
            .Select(row => KnowledgeArticleVersionDtoFactory.From(
                row, article.Version))
            .ToArray();
    }

    internal static async Task<Domain.Entities.KnowledgeArticle> LoadArticleAsync(
        IKnowledgeArticleRepository repository,
        ICurrentUserContext currentUser,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(articleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{articleId}' não encontrado.");

        if (article.Status.IsUnpublished()
            && !SearchKnowledgeArticlesQueryHandler.CanSeeUnpublished(currentUser))
            throw new KeyNotFoundException(
                $"Artigo '{articleId}' não encontrado.");

        return article;
    }
}

public sealed record CompareKnowledgeArticleVersionsQuery(
    Guid ArticleId,
    string From,
    string To) : IQuery<KnowledgeArticleVersionCompareDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class CompareKnowledgeArticleVersionsQueryHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeArticleVersionRepository versionRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<CompareKnowledgeArticleVersionsQuery,
        KnowledgeArticleVersionCompareDto>
{
    public async Task<KnowledgeArticleVersionCompareDto> Handle(
        CompareKnowledgeArticleVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var article = await ListKnowledgeArticleVersionsQueryHandler.LoadArticleAsync(
            repository, currentUser, request.ArticleId, cancellationToken);

        var from = await versionRepository.GetAsync(
                article.Id, request.From, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Versão '{request.From}' não encontrada.");
        var to = await versionRepository.GetAsync(
                article.Id, request.To, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Versão '{request.To}' não encontrada.");

        var nameIds = new[] { from.AuthorId, from.ApprovedById, to.AuthorId, to.ApprovedById }
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        var names = nameIds.Length == 0
            ? null
            : await repository.GetUserNamesAsync(nameIds, cancellationToken);

        return new KnowledgeArticleVersionCompareDto(
            KnowledgeArticleVersionDtoFactory.Snapshot(from, names),
            KnowledgeArticleVersionDtoFactory.Snapshot(to, names));
    }
}
