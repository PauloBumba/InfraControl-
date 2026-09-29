using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.NetworkInterfaces.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchNetworkInterfacesQuery(
    Guid? DeviceId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<NetworkInterfaceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchNetworkInterfacesQueryHandler(INetworkInterfaceRepository repository)
    : IRequestHandler<SearchNetworkInterfacesQuery, PagedResult<NetworkInterfaceDto>>
{
    public async Task<PagedResult<NetworkInterfaceDto>> Handle(
        SearchNetworkInterfacesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<NetworkInterfaceDto>(
            result.Items
                .Select(nic => new NetworkInterfaceDto(nic.Id, nic.DeviceId, nic.Name, nic.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetNetworkInterfaceQuery(Guid Id) : IQuery<NetworkInterfaceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetNetworkInterfaceQueryHandler(INetworkInterfaceRepository repository)
    : IRequestHandler<GetNetworkInterfaceQuery, NetworkInterfaceDto>
{
    public async Task<NetworkInterfaceDto> Handle(GetNetworkInterfaceQuery request, CancellationToken cancellationToken)
    {
        var nic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Interface '{request.Id}' não encontrado.");

        return new NetworkInterfaceDto(nic.Id, nic.DeviceId, nic.Name, nic.Description);
    }
}
