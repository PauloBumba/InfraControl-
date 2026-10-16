using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Virtualization.Commands;

// F31-T002 (docs/53 §4.2 / D4): vínculos VM → hypervisor → vCenter em
// colunas do próprio Device (FK self, 1:1 — sem tabela de junção, diferente
// do N:N SwitchPort↔Vlan do F3-T004). Erros nascem aqui/domínio em pt-BR e
// sobem no envelope via GlobalExceptionHandler: ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409.

/// <summary>Guarda de tipo da VM compartilhada pelos 4 handlers (404 pt-BR).</summary>
internal static class VirtualMachineGuards
{
    public static async Task<Device> GetVirtualMachineAsync(
        IDeviceRepository repository,
        Guid virtualMachineId,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(virtualMachineId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VirtualMachine '{virtualMachineId}' não encontrado.");

        if (device.Type != DeviceType.VirtualMachine)
            throw new KeyNotFoundException(
                $"VirtualMachine '{virtualMachineId}' não encontrado.");

        return device;
    }
}

public sealed record AssignHypervisorToVirtualMachineCommand(
    Guid VirtualMachineId,
    Guid HypervisorId) : BaseCommand<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AssignHypervisorToVirtualMachineCommandHandler(
    IDeviceRepository repository,
    ILogger<AssignHypervisorToVirtualMachineCommandHandler> logger)
    : IRequestHandler<AssignHypervisorToVirtualMachineCommand, DeviceDto>
{
    public async Task<DeviceDto> Handle(
        AssignHypervisorToVirtualMachineCommand request,
        CancellationToken cancellationToken)
    {
        var vm = await VirtualMachineGuards.GetVirtualMachineAsync(
            repository, request.VirtualMachineId, cancellationToken);

        var hypervisor = await repository.GetByIdAsync(request.HypervisorId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Hypervisor '{request.HypervisorId}' não encontrado.");

        if (hypervisor.Type == DeviceType.VirtualMachine)
            throw new ArgumentException(
                "Uma VM não pode ser hypervisor de outra VM.",
                nameof(request.HypervisorId));

        vm.LinkHypervisor(hypervisor.Id);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Hypervisor {HypervisorId} assigned to VirtualMachine {VirtualMachineId}.",
            hypervisor.Id, vm.Id);

        return CreateDeviceCommandHandler.ToDto(vm);
    }
}

public sealed record UnassignHypervisorFromVirtualMachineCommand(
    Guid VirtualMachineId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnassignHypervisorFromVirtualMachineCommandHandler(
    IDeviceRepository repository,
    ILogger<UnassignHypervisorFromVirtualMachineCommandHandler> logger)
    : IRequestHandler<UnassignHypervisorFromVirtualMachineCommand>
{
    public async Task Handle(
        UnassignHypervisorFromVirtualMachineCommand request,
        CancellationToken cancellationToken)
    {
        var vm = await VirtualMachineGuards.GetVirtualMachineAsync(
            repository, request.VirtualMachineId, cancellationToken);

        if (vm.HypervisorDeviceId is null)
            throw new KeyNotFoundException(
                $"Vínculo de hypervisor não encontrado para a VM '{request.VirtualMachineId}'.");

        vm.UnlinkHypervisor();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Hypervisor unassigned from VirtualMachine {VirtualMachineId}.",
            vm.Id);
    }
}

public sealed record AssignVCenterToVirtualMachineCommand(
    Guid VirtualMachineId,
    Guid VCenterId) : BaseCommand<DeviceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AssignVCenterToVirtualMachineCommandHandler(
    IDeviceRepository repository,
    ILogger<AssignVCenterToVirtualMachineCommandHandler> logger)
    : IRequestHandler<AssignVCenterToVirtualMachineCommand, DeviceDto>
{
    public async Task<DeviceDto> Handle(
        AssignVCenterToVirtualMachineCommand request,
        CancellationToken cancellationToken)
    {
        var vm = await VirtualMachineGuards.GetVirtualMachineAsync(
            repository, request.VirtualMachineId, cancellationToken);

        var vcenter = await repository.GetByIdAsync(request.VCenterId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"VCenter '{request.VCenterId}' não encontrado.");

        if (vcenter.Type != DeviceType.VCenter)
            throw new ArgumentException(
                $"O device '{request.VCenterId}' não é um VCenter.",
                nameof(request.VCenterId));

        vm.LinkVCenter(vcenter.Id);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "VCenter {VCenterId} assigned to VirtualMachine {VirtualMachineId}.",
            vcenter.Id, vm.Id);

        return CreateDeviceCommandHandler.ToDto(vm);
    }
}

public sealed record UnassignVCenterFromVirtualMachineCommand(
    Guid VirtualMachineId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnassignVCenterFromVirtualMachineCommandHandler(
    IDeviceRepository repository,
    ILogger<UnassignVCenterFromVirtualMachineCommandHandler> logger)
    : IRequestHandler<UnassignVCenterFromVirtualMachineCommand>
{
    public async Task Handle(
        UnassignVCenterFromVirtualMachineCommand request,
        CancellationToken cancellationToken)
    {
        var vm = await VirtualMachineGuards.GetVirtualMachineAsync(
            repository, request.VirtualMachineId, cancellationToken);

        if (vm.VCenterDeviceId is null)
            throw new KeyNotFoundException(
                $"Vínculo de vCenter não encontrado para a VM '{request.VirtualMachineId}'.");

        vm.UnlinkVCenter();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "VCenter unassigned from VirtualMachine {VirtualMachineId}.",
            vm.Id);
    }
}
