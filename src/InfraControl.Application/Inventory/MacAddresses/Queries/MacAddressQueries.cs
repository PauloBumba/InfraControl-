using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Inventory.MacAddresses.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchMacAddressesQuery(
    Guid? InterfaceId = null,
    Guid? DeviceId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<MacAddressDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class SearchMacAddressesQueryHandler(IMacAddressRepository repository)
    : IRequestHandler<SearchMacAddressesQuery, PagedResult<MacAddressDto>>
{
    public async Task<PagedResult<MacAddressDto>> Handle(
        SearchMacAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            request.InterfaceId, request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<MacAddressDto>(
            result.Items
                .Select(mac => new MacAddressDto(mac.Id, mac.InterfaceId, mac.Mac))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetMacAddressQuery(Guid Id) : IQuery<MacAddressDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class GetMacAddressQueryHandler(IMacAddressRepository repository)
    : IRequestHandler<GetMacAddressQuery, MacAddressDto>
{
    public async Task<MacAddressDto> Handle(GetMacAddressQuery request, CancellationToken cancellationToken)
    {
        var mac = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"MAC '{request.Id}' não encontrado.");

        return new MacAddressDto(mac.Id, mac.InterfaceId, mac.Mac);
    }
}
