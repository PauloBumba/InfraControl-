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
            includeDrafts: CanSeeDrafts(currentUser),
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

        return new PagedResult<KnowledgeArticleDto>(
            result.Items
                .Select(article => KnowledgeArticleDto.From(article, userNames))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    /// <summary>
    /// Rascunho só para quem escreve: papeis do usuário cortados em
    /// <see cref="RoleAccess.Operate"/> (Admin/Operator — ADR-016).
    /// </summary>
    internal static bool CanSeeDrafts(ICurrentUserContext currentUser)
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

        if (article.Status == KnowledgeArticleStatus.Draft
            && !SearchKnowledgeArticlesQueryHandler.CanSeeDrafts(currentUser))
            throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        var dto = await KnowledgeArticleDto.FromAsync(
            article, repository, cancellationToken);

        return dto with
        {
            // F10-T004: vínculos só no detalhe (a busca não detalha).
            DeviceIds = await deviceLinkRepository.GetDeviceIdsAsync(
                article.Id, cancellationToken),
            IncidentIds = await incidentLinkRepository.GetIncidentIdsAsync(
                article.Id, cancellationToken),
            ServiceIds = await serviceLinkRepository.GetServiceIdsAsync(
                article.Id, cancellationToken),
        };
    }
}
