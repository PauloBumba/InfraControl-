using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Virtualization.Commands;
using InfraControl.Application.Inventory.Virtualization.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F31-T002 (docs/53 §4.2 / D4): vínculos VM → hypervisor → vCenter em
/// colunas do próprio Device (FK self, 1 hypervisor e 1 vCenter por VM —
/// sem tabela de junção porque é 1:1). Guards de domínio em pt-BR (400),
/// guarda de tipo da VM em pt-BR (404), 409 na exclusão do device alvo
/// (molde HasPortsAsync) e busca "VMs do hypervisor" / "VMs do vCenter".
/// </summary>
public sealed class VirtualMachineLinkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VirtualMachineLinkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static AssignHypervisorToVirtualMachineCommandHandler AssignVmHandler(
        AppDbContext context)
        => new(new DeviceRepository(context),
            NullLogger<AssignHypervisorToVirtualMachineCommandHandler>.Instance);

    private static UnassignHypervisorFromVirtualMachineCommandHandler UnassignVmHandler(
        AppDbContext context)
        => new(new DeviceRepository(context),
            NullLogger<UnassignHypervisorFromVirtualMachineCommandHandler>.Instance);

    private static AssignVCenterToVirtualMachineCommandHandler AssignVCenterHandler(
        AppDbContext context)
        => new(new DeviceRepository(context),
            NullLogger<AssignVCenterToVirtualMachineCommandHandler>.Instance);

    private static UnassignVCenterFromVirtualMachineCommandHandler UnassignVCenterHandler(
        AppDbContext context)
        => new(new DeviceRepository(context),
            NullLogger<UnassignVCenterFromVirtualMachineCommandHandler>.Instance);

    private static DeleteDeviceCommandHandler DeleteHandler(AppDbContext context)
        => new(new DeviceRepository(context), NullLogger<DeleteDeviceCommandHandler>.Instance);

    private static SearchVirtualMachinesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new Application.Inventory.Devices.Queries.SearchDevicesQueryHandler(
                new DeviceRepository(context)),
            new DeviceRepository(context));

    private static async Task<Guid> SeedLocationAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    /// <summary>
    /// esx-01 (hypervisor), vcenter-01, vm-a (hypervisor + vCenter),
    /// vm-b (só hypervisor), vm-c (só vCenter), sw-01 (ruído).
    /// </summary>
    private static async Task<(Guid LocationId, Guid HypervisorId, Guid VCenterId,
        Guid VmA, Guid VmB, Guid VmC, Guid SwitchId)> SeedLinkedAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var hypervisor = Device.Create(location.Id, "esx-01", DeviceType.Server);
        var vcenter = Device.Create(location.Id, "vcenter-01", DeviceType.VCenter);
        var vmA = Device.Create(location.Id, "vm-a", DeviceType.VirtualMachine);
        var vmB = Device.Create(location.Id, "vm-b", DeviceType.VirtualMachine);
        var vmC = Device.Create(location.Id, "vm-c", DeviceType.VirtualMachine);
        var switchDevice = Device.Create(location.Id, "sw-01", DeviceType.Switch);

        vmA.LinkHypervisor(hypervisor.Id);
        vmA.LinkVCenter(vcenter.Id);
        vmB.LinkHypervisor(hypervisor.Id);
        vmC.LinkVCenter(vcenter.Id);

        context.Devices.AddRange(hypervisor, vcenter, vmA, vmB, vmC, switchDevice);
        await context.SaveChangesAsync();

        return (location.Id, hypervisor.Id, vcenter.Id,
            vmA.Id, vmB.Id, vmC.Id, switchDevice.Id);
    }

    // ---------- domínio ----------

    [Fact]
    public void LinkHypervisor_OnVirtualMachine_ShouldPersist()
    {
        var locationId = Guid.NewGuid();
        var host = Device.Create(locationId, "esx-01", DeviceType.Server);
        var vm = Device.Create(locationId, "vm-01", DeviceType.VirtualMachine);

        vm.LinkHypervisor(host.Id);

        vm.HypervisorDeviceId.Should().Be(host.Id);
    }

    [Fact]
    public void LinkHypervisor_OnNonVirtualMachine_ShouldThrowPtBr()
    {
        var locationId = Guid.NewGuid();
        var other = Device.Create(locationId, "esx-02", DeviceType.Server);
        var server = Device.Create(locationId, "srv-01", DeviceType.Server);

        var action = () => server.LinkHypervisor(other.Id);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Apenas VMs podem ter hypervisor vinculado.*");
    }

    [Fact]
    public void LinkHypervisor_ToSelf_ShouldThrowPtBr()
    {
        var vm = Device.Create(Guid.NewGuid(), "vm-01", DeviceType.VirtualMachine);

        var action = () => vm.LinkHypervisor(vm.Id);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Uma VM não pode ser hypervisor de si mesma.*");
    }

    [Fact]
    public void LinkVCenter_OnVirtualMachine_ShouldPersist()
    {
        var locationId = Guid.NewGuid();
        var vcenter = Device.Create(locationId, "vcenter-01", DeviceType.VCenter);
        var vm = Device.Create(locationId, "vm-01", DeviceType.VirtualMachine);

        vm.LinkVCenter(vcenter.Id);

        vm.VCenterDeviceId.Should().Be(vcenter.Id);
    }

    [Fact]
    public void LinkVCenter_OnNonVirtualMachine_ShouldThrowPtBr()
    {
        var locationId = Guid.NewGuid();
        var other = Device.Create(locationId, "vcenter-01", DeviceType.VCenter);
        var server = Device.Create(locationId, "srv-01", DeviceType.Server);

        var action = () => server.LinkVCenter(other.Id);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Apenas VMs podem ter vCenter vinculado.*");
    }

    [Fact]
    public void Unlink_ShouldClearBothLinks()
    {
        var locationId = Guid.NewGuid();
        var host = Device.Create(locationId, "esx-01", DeviceType.Server);
        var vcenter = Device.Create(locationId, "vcenter-01", DeviceType.VCenter);
        var vm = Device.Create(locationId, "vm-01", DeviceType.VirtualMachine);
        vm.LinkHypervisor(host.Id);
        vm.LinkVCenter(vcenter.Id);

        vm.UnlinkHypervisor();
        vm.UnlinkVCenter();

        vm.HypervisorDeviceId.Should().BeNull();
        vm.VCenterDeviceId.Should().BeNull();
    }

    // ---------- assign hypervisor ----------

    [Fact]
    public async Task AssignHypervisor_ToExistingServer_ShouldPersistLink()
    {
        await using var context = await CreateContextAsync();
        var (_, hostId, _, vmId, _, _, _) = await SeedLinkedAsync(context);
        // vm-d sem vínculo para o caso feliz do assign.
        var locationId = context.Devices.First().LocationId;
        var vmD = Device.Create(locationId, "vm-d", DeviceType.VirtualMachine);
        context.Devices.Add(vmD);
        await context.SaveChangesAsync();

        await AssignVmHandler(context).Handle(
            new AssignHypervisorToVirtualMachineCommand(vmD.Id, hostId),
            CancellationToken.None);

        var reloaded = await context.Devices.SingleAsync(d => d.Id == vmD.Id);
        reloaded.HypervisorDeviceId.Should().Be(hostId);
    }

    [Fact]
    public async Task AssignHypervisor_UnknownVm_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, hostId, _, _, _, _, _) = await SeedLinkedAsync(context);
        var unknownVmId = Guid.NewGuid();

        var action = () => AssignVmHandler(context).Handle(
            new AssignHypervisorToVirtualMachineCommand(unknownVmId, hostId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VirtualMachine '{unknownVmId}' não encontrado.");
    }

    [Fact]
    public async Task AssignHypervisor_SwitchAsVm_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, hostId, _, _, _, _, switchId) = await SeedLinkedAsync(context);

        var action = () => AssignVmHandler(context).Handle(
            new AssignHypervisorToVirtualMachineCommand(switchId, hostId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VirtualMachine '{switchId}' não encontrado.",
                "device que não é VM não pode receber vínculo via endpoint de VMs");
    }

    [Fact]
    public async Task AssignHypervisor_UnknownHypervisor_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.Add(vm);
        await context.SaveChangesAsync();
        var unknownHostId = Guid.NewGuid();

        var action = () => AssignVmHandler(context).Handle(
            new AssignHypervisorToVirtualMachineCommand(vm.Id, unknownHostId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Hypervisor '{unknownHostId}' não encontrado.");
    }

    [Fact]
    public async Task AssignHypervisor_TargetIsVirtualMachine_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var vmTarget = Device.Create(locationId, "vm-host", DeviceType.VirtualMachine);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.AddRange(vmTarget, vm);
        await context.SaveChangesAsync();

        var action = () => AssignVmHandler(context).Handle(
            new AssignHypervisorToVirtualMachineCommand(vm.Id, vmTarget.Id),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Uma VM não pode ser hypervisor de outra VM.*");
    }

    // ---------- assign vCenter ----------

    [Fact]
    public async Task AssignVCenter_ToExistingVCenter_ShouldPersistLink()
    {
        await using var context = await CreateContextAsync();
        var (_, _, vcenterId, _, _, _, _) = await SeedLinkedAsync(context);
        var locationId = await SeedLocationAsync(context);
        var vm = Device.Create(locationId, "vm-d", DeviceType.VirtualMachine);
        context.Devices.Add(vm);
        await context.SaveChangesAsync();

        await AssignVCenterHandler(context).Handle(
            new AssignVCenterToVirtualMachineCommand(vm.Id, vcenterId),
            CancellationToken.None);

        var reloaded = await context.Devices.SingleAsync(d => d.Id == vm.Id);
        reloaded.VCenterDeviceId.Should().Be(vcenterId);
    }

    [Fact]
    public async Task AssignVCenter_UnknownVCenter_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.Add(vm);
        await context.SaveChangesAsync();
        var unknownVCenterId = Guid.NewGuid();

        var action = () => AssignVCenterHandler(context).Handle(
            new AssignVCenterToVirtualMachineCommand(vm.Id, unknownVCenterId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VCenter '{unknownVCenterId}' não encontrado.");
    }

    [Fact]
    public async Task AssignVCenter_TargetIsServer_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var server = Device.Create(locationId, "srv-01", DeviceType.Server);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.AddRange(server, vm);
        await context.SaveChangesAsync();

        var action = () => AssignVCenterHandler(context).Handle(
            new AssignVCenterToVirtualMachineCommand(vm.Id, server.Id),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage($"O device '{server.Id}' não é um VCenter.*");
    }

    [Fact]
    public async Task AssignVCenter_UnknownVm_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, _, vcenterId, _, _, _, _) = await SeedLinkedAsync(context);
        var unknownVmId = Guid.NewGuid();

        var action = () => AssignVCenterHandler(context).Handle(
            new AssignVCenterToVirtualMachineCommand(unknownVmId, vcenterId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VirtualMachine '{unknownVmId}' não encontrado.");
    }

    // ---------- unassign ----------

    [Fact]
    public async Task UnassignHypervisor_WhenLinked_ShouldClear()
    {
        await using var context = await CreateContextAsync();
        var (_, _, _, vmId, _, _, _) = await SeedLinkedAsync(context);

        await UnassignVmHandler(context).Handle(
            new UnassignHypervisorFromVirtualMachineCommand(vmId),
            CancellationToken.None);

        var reloaded = await context.Devices.SingleAsync(d => d.Id == vmId);
        reloaded.HypervisorDeviceId.Should().BeNull();
    }

    [Fact]
    public async Task UnassignHypervisor_WhenNotLinked_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.Add(vm);
        await context.SaveChangesAsync();

        var action = () => UnassignVmHandler(context).Handle(
            new UnassignHypervisorFromVirtualMachineCommand(vm.Id),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo de hypervisor não encontrado para a VM '{vm.Id}'.");
    }

    [Fact]
    public async Task UnassignVCenter_WhenLinked_ShouldClear()
    {
        await using var context = await CreateContextAsync();
        var (_, _, _, vmId, _, _, _) = await SeedLinkedAsync(context);

        await UnassignVCenterHandler(context).Handle(
            new UnassignVCenterFromVirtualMachineCommand(vmId),
            CancellationToken.None);

        var reloaded = await context.Devices.SingleAsync(d => d.Id == vmId);
        reloaded.VCenterDeviceId.Should().BeNull();
    }

    [Fact]
    public async Task UnassignVCenter_WhenNotLinked_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var vm = Device.Create(locationId, "vm-x", DeviceType.VirtualMachine);
        context.Devices.Add(vm);
        await context.SaveChangesAsync();

        var action = () => UnassignVCenterHandler(context).Handle(
            new UnassignVCenterFromVirtualMachineCommand(vm.Id),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo de vCenter não encontrado para a VM '{vm.Id}'.");
    }

    // ---------- exclusão (409) ----------

    [Fact]
    public async Task DeleteDevice_HypervisorWithLinkedVm_ShouldThrow409PtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, hostId, _, _, _, _, _) = await SeedLinkedAsync(context);

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(hostId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem VMs vinculadas.");
    }

    [Fact]
    public async Task DeleteDevice_VCenterWithLinkedVm_ShouldThrow409PtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, _, vcenterId, _, _, _, _) = await SeedLinkedAsync(context);

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(vcenterId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem VMs vinculadas.");
    }

    [Fact]
    public async Task DeleteDevice_VirtualMachineWithLinks_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var (_, _, _, vmId, _, _, _) = await SeedLinkedAsync(context);

        await DeleteHandler(context).Handle(
            new DeleteDeviceCommand(vmId), CancellationToken.None);

        var exists = await context.Devices.AnyAsync(d => d.Id == vmId);
        exists.Should().BeFalse(
            "o backstop protege o device ALVO do vínculo; a própria VM é excluível");
    }

    // ---------- busca: VMs do hypervisor / do vCenter ----------

    [Fact]
    public async Task Search_ShouldFilterByHypervisor()
    {
        await using var context = await CreateContextAsync();
        var (_, hostId, _, _, _, _, _) = await SeedLinkedAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchVirtualMachinesQuery(HypervisorId: hostId),
            CancellationToken.None);

        result.TotalCount.Should().Be(2, "vm-a e vm-b rodam em esx-01");
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(["vm-a", "vm-b"]);
        result.Items.Should().OnlyContain(item => item.Type == "VirtualMachine");
    }

    [Fact]
    public async Task Search_ShouldFilterByVCenter()
    {
        await using var context = await CreateContextAsync();
        var (_, _, vcenterId, _, _, _, _) = await SeedLinkedAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchVirtualMachinesQuery(VCenterId: vcenterId),
            CancellationToken.None);

        result.TotalCount.Should().Be(2, "vm-a e vm-c gerenciadas pelo vcenter-01");
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(["vm-a", "vm-c"]);
    }

    [Fact]
    public async Task Search_WithoutLinkFilter_ShouldKeepTypeFixedBehavior()
    {
        await using var context = await CreateContextAsync();
        await SeedLinkedAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchVirtualMachinesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3, "sem filtro de vínculo, só muda o type fixo");
        result.Items.Should().OnlyContain(item => item.Type == "VirtualMachine");
    }
}
