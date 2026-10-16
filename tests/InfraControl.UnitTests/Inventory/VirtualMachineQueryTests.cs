using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Inventory.Virtualization.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F31-T001 (docs/53 §4.1): VMs e vCenter no inventário — queries dedicadas
/// delegam ao fluxo de Device (F2-T003) com type fixo (molde F3-T001
/// SearchSwitchesQuery): DeviceType.VirtualMachine = 12 / VCenter = 13 (D2 —
/// 11 ficou com Nvr no merge da F30-T005c; enum persiste como string) e
/// perfil default de Server (D3 — "VM entra na categoria de servidores").
/// Sem entidade nova e sem migração; guarda de tipo → 404 pt-BR.
/// </summary>
public sealed class VirtualMachineQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VirtualMachineQueryTests()
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

    private static SearchVirtualMachinesQueryHandler SearchVmHandler(AppDbContext context)
        => new(
            new SearchDevicesQueryHandler(new DeviceRepository(context)),
            new DeviceRepository(context));

    private static SearchVCentersQueryHandler SearchVCenterHandler(AppDbContext context)
        => new(new SearchDevicesQueryHandler(new DeviceRepository(context)));

    private static GetVirtualMachineQueryHandler GetVmHandler(AppDbContext context)
        => new(new DeviceRepository(context));

    private static GetVCenterQueryHandler GetVCenterHandler(AppDbContext context)
        => new(new DeviceRepository(context));

    private static async Task SeedDevicesAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        context.Devices.AddRange(
            Device.Create(location.Id, "vm-web-01", DeviceType.VirtualMachine, ip: "10.0.10.11"),
            Device.Create(location.Id, "vm-db-01", DeviceType.VirtualMachine),
            Device.Create(location.Id, "vcenter-01", DeviceType.VCenter, ip: "10.0.10.2"),
            Device.Create(location.Id, "sw-core", DeviceType.Switch),
            Device.Create(location.Id, "pr-01", DeviceType.Printer),
            Device.Create(location.Id, "srv-01", DeviceType.Server));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_ShouldReturnOnlyVirtualMachines()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);

        var result = await SearchVmHandler(context).Handle(
            new SearchVirtualMachinesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(item => item.Type == "VirtualMachine",
            "a busca de VMs deve fixar type=VirtualMachine e ignorar demais tipos");
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(
            ["vm-web-01", "vm-db-01"]);
    }

    [Fact]
    public async Task Search_ShouldApplyTermTogetherWithType()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);

        var result = await SearchVmHandler(context).Handle(
            new SearchVirtualMachinesQuery(Term: "vm-web"), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("vm-web-01");
    }

    [Fact]
    public async Task SearchVCenters_ShouldReturnOnlyVCenters()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);

        var result = await SearchVCenterHandler(context).Handle(
            new SearchVCentersQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(1,
            "só o vcenter-01 tem type=VCenter; devices comuns não podem aparecer");
        result.Items.Single().Type.Should().Be("VCenter");
        result.Items.Single().Name.Should().Be("vcenter-01");
    }

    [Fact]
    public async Task Get_ExistingVirtualMachine_ShouldReturnDeviceDto()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);
        var vmId = context.Devices.Single(device => device.Name == "vm-web-01").Id;

        var result = await GetVmHandler(context).Handle(
            new GetVirtualMachineQuery(vmId), CancellationToken.None);

        result.Id.Should().Be(vmId);
        result.Name.Should().Be("vm-web-01");
        result.Type.Should().Be(nameof(DeviceType.VirtualMachine));
        result.Ip.Should().Be("10.0.10.11");
    }

    [Fact]
    public async Task Get_SwitchAsVirtualMachine_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);
        var switchId = context.Devices.Single(device => device.Name == "sw-core").Id;

        var action = () => GetVmHandler(context).Handle(
            new GetVirtualMachineQuery(switchId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VirtualMachine '{switchId}' não encontrado.",
                "device que não é VM não pode ser exposto pelo endpoint de VMs");
    }

    [Fact]
    public async Task Get_UnknownVirtualMachineId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => GetVmHandler(context).Handle(
            new GetVirtualMachineQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VirtualMachine '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Get_ExistingVCenter_ShouldReturnDeviceDto()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);
        var vcenterId = context.Devices.Single(device => device.Name == "vcenter-01").Id;

        var result = await GetVCenterHandler(context).Handle(
            new GetVCenterQuery(vcenterId), CancellationToken.None);

        result.Id.Should().Be(vcenterId);
        result.Name.Should().Be("vcenter-01");
        result.Type.Should().Be(nameof(DeviceType.VCenter));
    }

    [Fact]
    public async Task Get_UnknownVCenterId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => GetVCenterHandler(context).Handle(
            new GetVCenterQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VCenter '{unknownId}' não encontrado.");
    }
}
