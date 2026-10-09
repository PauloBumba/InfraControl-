using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Knowledge.Articles.Queries;

// Sem cache (IQuery direto): artigo muda a cada mutation.
<<<<<<< HEAD
// F24-T002/K2 (ADR-034 §4): estados nunca-publicados (Draft, InReview,
// Approved, Rejected) só aparecem para quem tem nível de escrita
// (ADR-016 ≡ Operate) — a busca filtra e o GET/{id} de um desses
// estados responde 404 como se não existisse (não vaza existência).
=======
// F10-T003: rascunho (Draft) só aparece para quem tem nível de
// escrita (ADR-016 ≡ Operate) — a busca filtra e o GET/{id} de um
// Draft responde 404 como se não existisse (não vaza existência).
// F24-T001/K1: autor/responsável resolvidos em lote (join em
// DomainUsers) — 1 chamada por request, sem N+1.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

public sealed record SearchKnowledgeArticlesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20,
    // F24-T005/K5 (docs/32 §12): filtro "pendentes de revisão" —
    // próxima revisão vencida ("Knowledge necessita de revisão").
    bool PendingReview = false,
    // F24-T007/K7 (docs/32 §13): busca avançada — filtros estruturados
    // AND sobre campos que existem no modelo. "Sistema"/"setor" da spec
    // não existem em nenhuma entidade e ficam documentados como ausentes.
    Guid? CategoryId = null,
    Guid? AuthorId = null,
    KnowledgeArticleType? Type = null,
    string? Tag = null) : IQuery<PagedResult<KnowledgeArticleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchKnowledgeArticlesQueryHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<SearchKnowledgeArticlesQuery, PagedResult<KnowledgeArticleDto>>
{
    public async Task<PagedResult<KnowledgeArticleDto>> Handle(
        SearchKnowledgeArticlesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();
        // Tag vazia/branca vira null (o repositório normaliza p/ lowercase).
        var tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();

        var result = await repository.SearchAsync(
            term, page, pageSize,
            includeUnpublished: CanSeeUnpublished(currentUser),
<<<<<<< HEAD
            pendingReview: request.PendingReview,
            categoryId: request.CategoryId,
            authorId: request.AuthorId,
            type: request.Type,
            tag: tag,
=======
            cancellationToken);

        var names = await KnowledgeArticleQueries.ResolveUserNamesAsync(
            repository,
            result.Items.SelectMany(article =>
                new[] { article.AuthorId, article.ResponsibleId }),
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            cancellationToken);

        // F24-T001/K1: nomes de autor/responsável da página inteira num
        // round-trip só (group do batch — sem N+1).
        var nameIds = result.Items
            .SelectMany(KnowledgeArticleDto.NameIds)
            .Distinct()
            .ToArray();
        var userNames = nameIds.Length == 0
            ? null
            : await repository.GetUserNamesAsync(nameIds, cancellationToken);

        // F24-T004/K4: origens da página inteira no mesmo critério —
        // um round-trip e o group por artigo (docs/32 §9).
        var originsByArticle = (await originRepository.ListByArticleIdsAsync(
                result.Items.Select(article => article.Id).ToArray(), cancellationToken))
            .GroupBy(origin => origin.ArticleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(origin => new KnowledgeArticleOriginDto(
                        origin.OriginType.ToString(), origin.OriginId))
                    .ToArray());

        return new PagedResult<KnowledgeArticleDto>(
            result.Items
<<<<<<< HEAD
                .Select(article =>
                {
                    var dto = KnowledgeArticleDto.From(article, userNames);
                    originsByArticle.TryGetValue(article.Id, out var origins);
                    return dto with { Origins = origins ?? [] };
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
                })
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    /// <summary>
    /// Nunca-publicados só para quem escreve: papeis do usuário
    /// cortados em <see cref="RoleAccess.Operate"/> (Admin/Operator —
    /// ADR-016; regra F10-T003 generalizada — ADR-034 §4).
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
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetKnowledgeArticleQuery, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GetKnowledgeArticleQuery request, CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

<<<<<<< HEAD
        // ADR-034 §4: estado nunca-publicado sem escrita = 404 mesmo
        // (não vaza existência — regra F10-T003 generalizada).
        if (article.Status.IsUnpublished()
=======
        if (!article.Status.IsVisibleToReaders()
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            && !SearchKnowledgeArticlesQueryHandler.CanSeeUnpublished(currentUser))
            throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

<<<<<<< HEAD
        var dto = await KnowledgeArticleDto.FromAsync(
            article, repository, cancellationToken);

        return dto with
        {
            // F10-T004: vínculos só no detalhe (a busca não detalha)…
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            DeviceIds = await deviceLinkRepository.GetDeviceIdsAsync(
                article.Id, cancellationToken),
            IncidentIds = await incidentLinkRepository.GetIncidentIdsAsync(
                article.Id, cancellationToken),
            ServiceIds = await serviceLinkRepository.GetServiceIdsAsync(
                article.Id, cancellationToken),
<<<<<<< HEAD
            // …exceto as origens (K4), que a busca também traz em lote.
            Origins = (await originRepository.ListByArticleAsync(
                    article.Id, cancellationToken))
                .Select(origin => new KnowledgeArticleOriginDto(
                    origin.OriginType.ToString(), origin.OriginId))
                .ToArray(),
=======
            AuthorName = KnowledgeArticleQueries.NameOf(names, article.AuthorId),
            ResponsibleName = KnowledgeArticleQueries.NameOf(names, article.ResponsibleId),
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
