using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.Locations.Queries;

// Sem cache (IQuery direto, não BaseQuery): inventário muda a cada mutation e
// servir lista/entidade obsoleta após Create/Update quebraria a UX — o
// CachingBehavior só age em ICacheableQuery.

public sealed record SearchLocationsQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<LocationDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchLocationsQueryHandler(ILocationRepository repository)
    : IRequestHandler<SearchLocationsQuery, PagedResult<LocationDto>>
{
    public async Task<PagedResult<LocationDto>> Handle(
        SearchLocationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(term, page, pageSize, cancellationToken);

        return new PagedResult<LocationDto>(
            result.Items
                .Select(location => new LocationDto(location.Id, location.Name, location.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetLocationQuery(Guid Id) : IQuery<LocationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetLocationQueryHandler(ILocationRepository repository)
    : IRequestHandler<GetLocationQuery, LocationDto>
{
    public async Task<LocationDto> Handle(GetLocationQuery request, CancellationToken cancellationToken)
    {
        var location = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Local '{request.Id}' não encontrado.");

        return new LocationDto(location.Id, location.Name, location.Description);
    }
}
