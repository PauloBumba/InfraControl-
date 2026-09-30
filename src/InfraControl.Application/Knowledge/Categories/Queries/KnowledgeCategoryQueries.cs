using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Knowledge.Categories.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Knowledge.Categories.Queries;

// Sem cache (IQuery direto): categoria muda a cada mutation.

public sealed record SearchKnowledgeCategoriesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<KnowledgeCategoryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchKnowledgeCategoriesQueryHandler(IKnowledgeCategoryRepository repository)
    : IRequestHandler<SearchKnowledgeCategoriesQuery, PagedResult<KnowledgeCategoryDto>>
{
    public async Task<PagedResult<KnowledgeCategoryDto>> Handle(
        SearchKnowledgeCategoriesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(term, page, pageSize, cancellationToken);

        return new PagedResult<KnowledgeCategoryDto>(
            result.Items
                .Select(category => new KnowledgeCategoryDto(
                    category.Id, category.Name, category.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetKnowledgeCategoryQuery(Guid Id)
    : IQuery<KnowledgeCategoryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetKnowledgeCategoryQueryHandler(IKnowledgeCategoryRepository repository)
    : IRequestHandler<GetKnowledgeCategoryQuery, KnowledgeCategoryDto>
{
    public async Task<KnowledgeCategoryDto> Handle(
        GetKnowledgeCategoryQuery request, CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Categoria '{request.Id}' não encontrada.");

        return new KnowledgeCategoryDto(category.Id, category.Name, category.Description);
    }
}
