using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Inventory.Racks.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchRacksQuery(
    Guid? LocationId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<RackDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class SearchRacksQueryHandler(IRackRepository repository)
    : IRequestHandler<SearchRacksQuery, PagedResult<RackDto>>
{
    public async Task<PagedResult<RackDto>> Handle(
        SearchRacksQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(request.LocationId, term, page, pageSize, cancellationToken);

        return new PagedResult<RackDto>(
            result.Items
                .Select(rack => new RackDto(rack.Id, rack.LocationId, rack.Name, rack.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetRackQuery(Guid Id) : IQuery<RackDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class GetRackQueryHandler(IRackRepository repository)
    : IRequestHandler<GetRackQuery, RackDto>
{
    public async Task<RackDto> Handle(GetRackQuery request, CancellationToken cancellationToken)
    {
        var rack = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Rack '{request.Id}' não encontrado.");

        return new RackDto(rack.Id, rack.LocationId, rack.Name, rack.Description);
    }
}
