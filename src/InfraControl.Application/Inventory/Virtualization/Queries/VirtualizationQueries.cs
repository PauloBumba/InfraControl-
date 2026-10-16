using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
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
    Guid? RackId = null,
    Guid? HypervisorId = null,
    Guid? VCenterId = null) : IQuery<PagedResult<DeviceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchVirtualMachinesQueryHandler(
    SearchDevicesQueryHandler devices,
    IDeviceRepository repository)
    : IRequestHandler<SearchVirtualMachinesQuery, PagedResult<DeviceDto>>
{
    public async Task<PagedResult<DeviceDto>> Handle(
        SearchVirtualMachinesQuery request,
        CancellationToken cancellationToken)
    {
        // F31-T002: "VMs do hypervisor" / "VMs do vCenter" — filtro de vínculo
        // (local/rack não se aplicam a "VMs sob um host"); sem filtro, o
        // caminho do F31-T001 continua intocado (type fixo, sem cache).
        if (request.HypervisorId is not null || request.VCenterId is not null)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var linked = await repository.SearchVirtualMachinesByLinkAsync(
                request.HypervisorId,
                request.VCenterId,
                request.Term,
                page,
                pageSize,
                cancellationToken);

            return new PagedResult<DeviceDto>(
                linked.Items.Select(CreateDeviceCommandHandler.ToDto).ToArray(),
                linked.TotalCount,
                linked.Page,
                linked.PageSize);
        }

        return await devices.Handle(
            new SearchDevicesQuery(
                request.LocationId,
                request.RackId,
                Type: "VirtualMachine",
                request.Term,
                request.Page,
                request.PageSize),
            cancellationToken);
    }
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

// F31-T003 (docs/53 §4.3): visão agregada da virtualização — read-model em
// memória sobre o repositório de devices (molde F3-T006 topologia), sem
// entidade nova e sem migração.

/// <summary>Visão agregada da virtualização.</summary>
public sealed record VirtualizationOverviewDto(
    int TotalVirtualMachines,
    int TotalVCenters,
    int OrphanVirtualMachines,
    VirtualizationGroupDto[] ByHypervisor,
    VirtualizationGroupDto[] ByVCenter,
    VirtualMachineOverviewItemDto[] VirtualMachines);

/// <summary>Grupo de VMs sob um hypervisor (ou um vCenter) — nome resolvido.</summary>
public sealed record VirtualizationGroupDto(
    Guid DeviceId,
    string Name,
    int VirtualMachineCount);

/// <summary>VM com os vínculos resolvidos por nome.</summary>
public sealed record VirtualMachineOverviewItemDto(
    Guid Id,
    string Name,
    string? Ip,
    Guid? HypervisorId,
    string? HypervisorName,
    Guid? VCenterId,
    string? VCenterName,
    bool IsOrphan);

public sealed record GetVirtualizationOverviewQuery
    : IQuery<VirtualizationOverviewDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetVirtualizationOverviewQueryHandler(IDeviceRepository repository)
    : IRequestHandler<GetVirtualizationOverviewQuery, VirtualizationOverviewDto>
{
    public async Task<VirtualizationOverviewDto> Handle(
        GetVirtualizationOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var all = await repository.GetAllOrderedByNameAsync(cancellationToken);
        var byId = all.ToDictionary(device => device.Id);

        string? ResolveName(Guid? deviceId) =>
            deviceId is Guid key && byId.TryGetValue(key, out var device)
                ? device.Name
                : null;

        var virtualMachines = all
            .Where(device => device.Type == DeviceType.VirtualMachine)
            .ToList();

        var byHypervisor = virtualMachines
            .Where(vm => vm.HypervisorDeviceId is not null)
            .GroupBy(vm => vm.HypervisorDeviceId!.Value)
            .Select(group => new VirtualizationGroupDto(
                group.Key,
                ResolveName(group.Key) ?? group.Key.ToString(),
                group.Count()))
            .OrderBy(g => g.Name, StringComparer.Ordinal)
            .ToArray();

        var byVCenter = virtualMachines
            .Where(vm => vm.VCenterDeviceId is not null)
            .GroupBy(vm => vm.VCenterDeviceId!.Value)
            .Select(group => new VirtualizationGroupDto(
                group.Key,
                ResolveName(group.Key) ?? group.Key.ToString(),
                group.Count()))
            .OrderBy(g => g.Name, StringComparer.Ordinal)
            .ToArray();

        bool IsOrphan(Device vm) =>
            vm.HypervisorDeviceId is null && vm.VCenterDeviceId is null;

        var items = virtualMachines
            .Select(vm => new VirtualMachineOverviewItemDto(
                vm.Id,
                vm.Name,
                vm.Ip,
                vm.HypervisorDeviceId,
                ResolveName(vm.HypervisorDeviceId),
                vm.VCenterDeviceId,
                ResolveName(vm.VCenterDeviceId),
                IsOrphan(vm)))
            .ToArray();

        return new VirtualizationOverviewDto(
            virtualMachines.Count,
            all.Count(device => device.Type == DeviceType.VCenter),
            virtualMachines.Count(IsOrphan),
            byHypervisor,
            byVCenter,
            items);
    }
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
