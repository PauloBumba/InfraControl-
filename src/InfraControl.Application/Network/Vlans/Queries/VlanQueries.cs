using InfraControl.Application.Network.Vlans.Ports;

namespace InfraControl.Application.Network.Vlans.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation. Ordenação por
// VlanNumber (ordem operacional 802.1Q), não alfabética.

public sealed record SearchVlansQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<VlanDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class SearchVlansQueryHandler(IVlanRepository repository)
    : IRequestHandler<SearchVlansQuery, PagedResult<VlanDto>>
{
    public async Task<PagedResult<VlanDto>> Handle(
        SearchVlansQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(term, page, pageSize, cancellationToken);

        return new PagedResult<VlanDto>(
            result.Items
                .Select(vlan => new VlanDto(vlan.Id, vlan.Name, vlan.VlanNumber, vlan.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetVlanQuery(Guid Id) : IQuery<VlanDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class GetVlanQueryHandler(IVlanRepository repository)
    : IRequestHandler<GetVlanQuery, VlanDto>
{
    public async Task<VlanDto> Handle(GetVlanQuery request, CancellationToken cancellationToken)
    {
        var vlan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"VLAN '{request.Id}' não encontrada.");

        return new VlanDto(vlan.Id, vlan.Name, vlan.VlanNumber, vlan.Description);
    }
}
