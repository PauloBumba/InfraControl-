using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Inventory.Virtualization.Queries;

// F31-T001 (docs/53 §4.1): VMs e vCenter são Device com type próprio
// (D2 — mesma ideia do F3-T001: Switch = Device type=Switch). REUSO puro do
// fluxo F2-T003: Search delega ao SearchDevicesQueryHandler com type fixo; Get
// usa o mesmo repositório e o mesmo mapper ToDto, acrescentando só a guarda de
// tipo. Sem cache (IQuery direto, não BaseQuery): inventário muda a cada
// mutation — mesmo motivo documentado em DeviceQueries/LocationQueries.

public sealed record SearchVirtualMachinesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20,
    Guid? LocationId = null,
    Guid? RackId = null) : IQuery<PagedResult<DeviceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchVirtualMachinesQueryHandler(SearchDevicesQueryHandler devices)
    : IRequestHandler<SearchVirtualMachinesQuery, PagedResult<DeviceDto>>
{
    public Task<PagedResult<DeviceDto>> Handle(
        SearchVirtualMachinesQuery request,
        CancellationToken cancellationToken)
        => devices.Handle(
            new SearchDevicesQuery(
                request.LocationId,
                request.RackId,
                Type: "VirtualMachine",
                request.Term,
                request.Page,
                request.PageSize),
            cancellationToken);
}

public sealed record SearchVCentersQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DeviceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchVCentersQueryHandler(SearchDevicesQueryHandler devices)
    : IRequestHandler<SearchVCentersQuery, PagedResult<DeviceDto>>
{
    public Task<PagedResult<DeviceDto>> Handle(
        SearchVCentersQuery request,
        CancellationToken cancellationToken)
        => devices.Handle(
            new SearchDevicesQuery(
                Type: "VCenter",
                Term: request.Term,
                Page: request.Page,
                PageSize: request.PageSize),
            cancellationToken);
}

public sealed record GetVirtualMachineQuery(Guid Id) : IQuery<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetVirtualMachineQueryHandler(IDeviceRepository repository)
    : IRequestHandler<GetVirtualMachineQuery, DeviceDto>
{
    public async Task<DeviceDto> Handle(
        GetVirtualMachineQuery request,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VirtualMachine '{request.Id}' não encontrado.");

        if (device.Type != DeviceType.VirtualMachine)
            throw new KeyNotFoundException(
                $"VirtualMachine '{request.Id}' não encontrado.");

        return CreateDeviceCommandHandler.ToDto(device);
    }
}

public sealed record GetVCenterQuery(Guid Id) : IQuery<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetVCenterQueryHandler(IDeviceRepository repository)
    : IRequestHandler<GetVCenterQuery, DeviceDto>
{
    public async Task<DeviceDto> Handle(
        GetVCenterQuery request,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VCenter '{request.Id}' não encontrado.");

        if (device.Type != DeviceType.VCenter)
            throw new KeyNotFoundException(
                $"VCenter '{request.Id}' não encontrado.");

        return CreateDeviceCommandHandler.ToDto(device);
    }
}
