using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Knowledge.Articles.Queries;

// Sem cache (IQuery direto): artigo muda a cada mutation.

public sealed record SearchKnowledgeArticlesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<KnowledgeArticleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchKnowledgeArticlesQueryHandler(IKnowledgeArticleRepository repository)
    : IRequestHandler<SearchKnowledgeArticlesQuery, PagedResult<KnowledgeArticleDto>>
{
    public async Task<PagedResult<KnowledgeArticleDto>> Handle(
        SearchKnowledgeArticlesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(term, page, pageSize, cancellationToken);

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
}

public sealed record GetKnowledgeArticleQuery(Guid Id)
    : IQuery<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetKnowledgeArticleQueryHandler(IKnowledgeArticleRepository repository)
    : IRequestHandler<GetKnowledgeArticleQuery, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GetKnowledgeArticleQuery request, CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content,
            article.CategoryId, article.Status);
    }
}
