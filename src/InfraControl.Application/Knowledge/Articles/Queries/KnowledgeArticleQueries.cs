using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Knowledge.Articles.Queries;

// Sem cache (IQuery direto): artigo muda a cada mutation.
// F10-T003: rascunho (Draft) só aparece para quem tem nível de
// escrita (ADR-016 ≡ Operate) — a busca filtra e o GET/{id} de um
// Draft responde 404 como se não existisse (não vaza existência).
// F24-T001/K1: autor/responsável resolvidos em lote (join em
// DomainUsers) — 1 chamada por request, sem N+1.

public sealed record SearchKnowledgeArticlesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<KnowledgeArticleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchKnowledgeArticlesQueryHandler(
    IKnowledgeArticleRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<SearchKnowledgeArticlesQuery, PagedResult<KnowledgeArticleDto>>
{
    public async Task<PagedResult<KnowledgeArticleDto>> Handle(
        SearchKnowledgeArticlesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            term, page, pageSize,
            includeUnpublished: CanSeeUnpublished(currentUser),
            cancellationToken);

        var names = await KnowledgeArticleQueries.ResolveUserNamesAsync(
            repository,
            result.Items.SelectMany(article =>
                new[] { article.AuthorId, article.ResponsibleId }),
            cancellationToken);

        return new PagedResult<KnowledgeArticleDto>(
            result.Items
                .Select(article => new KnowledgeArticleDto(
                    article.Id, article.Title, article.Content,
                    article.CategoryId, article.Status,
                    article.Type, article.Description,
                    article.AuthorId, article.ResponsibleId)
                {
                    Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
                    AuthorName = KnowledgeArticleQueries.NameOf(
                        names, article.AuthorId),
                    ResponsibleName = KnowledgeArticleQueries.NameOf(
                        names, article.ResponsibleId),
                })
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    /// <summary>
    /// Rascunho só para quem escreve: papeis do usuário cortados em
    /// <see cref="RoleAccess.Operate"/> (Admin/Operator — ADR-016).
    /// </summary>
    internal static bool CanSeeUnpublished(ICurrentUserContext currentUser)
        => RoleAccess.Operate.Any(role => currentUser.Roles.Contains(role));
}

public sealed record GetKnowledgeArticleQuery(Guid Id)
    : IQuery<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetKnowledgeArticleQueryHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeArticleDeviceRepository deviceLinkRepository,
    IKnowledgeArticleIncidentRepository incidentLinkRepository,
    IKnowledgeArticleServiceRepository serviceLinkRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetKnowledgeArticleQuery, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GetKnowledgeArticleQuery request, CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        if (!article.Status.IsVisibleToReaders()
            && !SearchKnowledgeArticlesQueryHandler.CanSeeUnpublished(currentUser))
            throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        var names = await KnowledgeArticleQueries.ResolveUserNamesAsync(
            repository,
            [article.AuthorId, article.ResponsibleId],
            cancellationToken);

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content,
            article.CategoryId, article.Status,
            article.Type, article.Description,
            article.AuthorId, article.ResponsibleId)
        {
            // F10-T004: vínculos só no detalhe (a busca não detalha).
            Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
            DeviceIds = await deviceLinkRepository.GetDeviceIdsAsync(
                article.Id, cancellationToken),
            IncidentIds = await incidentLinkRepository.GetIncidentIdsAsync(
                article.Id, cancellationToken),
            ServiceIds = await serviceLinkRepository.GetServiceIdsAsync(
                article.Id, cancellationToken),
            AuthorName = KnowledgeArticleQueries.NameOf(names, article.AuthorId),
            ResponsibleName = KnowledgeArticleQueries.NameOf(names, article.ResponsibleId),
        };
    }
}

/// <summary>
/// Resolução de nomes do K1 — compartilhada por Search/Get (1 chamada,
/// ids distintos; sem N+1, precedente GetUserNamesAsync de Tasks/PDI).
/// </summary>
internal static class KnowledgeArticleQueries
{
    internal static async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(
        IKnowledgeArticleRepository repository,
        IEnumerable<Guid?> candidateIds,
        CancellationToken cancellationToken)
    {
        var userIds = candidateIds
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
            return new Dictionary<Guid, string>();

        return await repository.GetUserNamesAsync(userIds, cancellationToken);
    }

    internal static string? NameOf(
        IReadOnlyDictionary<Guid, string> names, Guid? userId)
        => userId is { } id && names.TryGetValue(id, out var name) ? name : null;
}
