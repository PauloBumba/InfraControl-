using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Inventory.Devices.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchDevicesQuery(
    Guid? LocationId = null,
    Guid? RackId = null,
    string? Type = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DeviceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchDevicesQueryHandler(IDeviceRepository repository)
    : IRequestHandler<SearchDevicesQuery, PagedResult<DeviceDto>>
{
    public async Task<PagedResult<DeviceDto>> Handle(
        SearchDevicesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();
        DeviceType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            type = CreateDeviceCommandHandler.ParseType(request.Type);
        }

        var result = await repository.SearchAsync(
            request.LocationId, request.RackId, type, term, page, pageSize, cancellationToken);

        return new PagedResult<DeviceDto>(
            result.Items.Select(CreateDeviceCommandHandler.ToDto).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetDeviceQuery(Guid Id) : IQuery<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetDeviceQueryHandler(IDeviceRepository repository)
    : IRequestHandler<GetDeviceQuery, DeviceDto>
{
    public async Task<DeviceDto> Handle(GetDeviceQuery request, CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        return CreateDeviceCommandHandler.ToDto(device);
    }
}
