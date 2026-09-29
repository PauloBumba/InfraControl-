using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Switches.Queries;

// F3-T001: módulo Network — extensão de Device tipo Switch (docs/03: não existe
// entidade Switch, é Device com type=Switch). REUSO puro do fluxo F2-T003:
// Search delega ao SearchDevicesQueryHandler com type fixo; Get usa o mesmo
// repositório e o mesmo mapper ToDto, acrescentando só a guarda de tipo.

public sealed record SearchSwitchesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20,
    Guid? LocationId = null,
    Guid? RackId = null) : IQuery<PagedResult<DeviceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSwitchesQueryHandler(SearchDevicesQueryHandler devices)
    : IRequestHandler<SearchSwitchesQuery, PagedResult<DeviceDto>>
{
    public Task<PagedResult<DeviceDto>> Handle(
        SearchSwitchesQuery request,
        CancellationToken cancellationToken)
        => devices.Handle(
            new SearchDevicesQuery(
                request.LocationId,
                request.RackId,
                Type: "Switch",
                request.Term,
                request.Page,
                request.PageSize),
            cancellationToken);
}

public sealed record GetSwitchQuery(Guid Id) : IQuery<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSwitchQueryHandler(IDeviceRepository repository)
    : IRequestHandler<GetSwitchQuery, DeviceDto>
{
    public async Task<DeviceDto> Handle(GetSwitchQuery request, CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Switch '{request.Id}' não encontrado.");

        if (device.Type != DeviceType.Switch)
            throw new KeyNotFoundException($"Switch '{request.Id}' não encontrado.");

        return CreateDeviceCommandHandler.ToDto(device);
    }
}
