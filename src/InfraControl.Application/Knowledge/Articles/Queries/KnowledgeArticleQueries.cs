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

        return new PagedResult<KnowledgeArticleDto>(
            result.Items
                .Select(article => new KnowledgeArticleDto(
                    article.Id, article.Title, article.Content,
                    article.CategoryId, article.Status))
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

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content,
            article.CategoryId, article.Status);
    }
}
