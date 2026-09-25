using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Inventory.IpAddresses.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchIpAddressesQuery(
    Guid? InterfaceId = null,
    Guid? DeviceId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<IpAddressDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class SearchIpAddressesQueryHandler(IIpAddressRepository repository)
    : IRequestHandler<SearchIpAddressesQuery, PagedResult<IpAddressDto>>
{
    public async Task<PagedResult<IpAddressDto>> Handle(
        SearchIpAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            request.InterfaceId, request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<IpAddressDto>(
            result.Items
                .Select(entity => new IpAddressDto(entity.Id, entity.InterfaceId, entity.Ip))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetIpAddressQuery(Guid Id) : IQuery<IpAddressDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class GetIpAddressQueryHandler(IIpAddressRepository repository)
    : IRequestHandler<GetIpAddressQuery, IpAddressDto>
{
    public async Task<IpAddressDto> Handle(GetIpAddressQuery request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"IP '{request.Id}' não encontrado.");

        return new IpAddressDto(entity.Id, entity.InterfaceId, entity.Ip);
    }
}
