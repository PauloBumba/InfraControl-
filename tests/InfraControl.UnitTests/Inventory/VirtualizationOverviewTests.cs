using FluentAssertions;
using InfraControl.Application.Inventory.Virtualization.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F31-T003 (docs/53 §4.3): visão agregada da virtualização — contagem de
/// VMs por hypervisor e por vCenter, órfãs (VM sem nenhum vínculo) e
/// listagem de VMs com os nomes dos vínculos resolvidos. Read-model em
/// memória sobre o repositório de devices (molde F3-T006 topologia),
/// sem entidade nova e sem migração.
/// </summary>
public sealed class VirtualizationOverviewTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VirtualizationOverviewTests()
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

    private static GetVirtualizationOverviewQueryHandler OverviewHandler(AppDbContext context)
        => new(new DeviceRepository(context));

    /// <summary>
    /// esx-01 ← vm-a (hypervisor+vCenter), vm-b (só hypervisor);
    /// vcenter-01 ← vm-a, vm-c (só vCenter); vm-o órfã;
    /// srv-01 e sw-01 são ruído (não são VM nem aparecem nos grupos).
    /// </summary>
    private static async Task SeedAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var hypervisor = Device.Create(location.Id, "esx-01", DeviceType.Server);
        var vcenter = Device.Create(location.Id, "vcenter-01", DeviceType.VCenter);
        var vmA = Device.Create(location.Id, "vm-a", DeviceType.VirtualMachine, ip: "10.0.10.11");
        var vmB = Device.Create(location.Id, "vm-b", DeviceType.VirtualMachine);
        var vmC = Device.Create(location.Id, "vm-c", DeviceType.VirtualMachine);
        var vmOrphan = Device.Create(location.Id, "vm-o", DeviceType.VirtualMachine);
        var server = Device.Create(location.Id, "srv-01", DeviceType.Server);
        var switchDevice = Device.Create(location.Id, "sw-01", DeviceType.Switch);

        vmA.LinkHypervisor(hypervisor.Id);
        vmA.LinkVCenter(vcenter.Id);
        vmB.LinkHypervisor(hypervisor.Id);
        vmC.LinkVCenter(vcenter.Id);

        context.Devices.AddRange(
            hypervisor, vcenter, vmA, vmB, vmC, vmOrphan, server, switchDevice);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Overview_ShouldCountTotals()
    {
        await using var context = await CreateContextAsync();
        await SeedAsync(context);

        var result = await OverviewHandler(context).Handle(
            new GetVirtualizationOverviewQuery(), CancellationToken.None);

        result.TotalVirtualMachines.Should().Be(4, "vm-a, vm-b, vm-c e vm-o");
        result.TotalVCenters.Should().Be(1, "só vcenter-01 tem type=VCenter");
        result.OrphanVirtualMachines.Should().Be(1,
            "só vm-o não tem hypervisor nem vCenter");
    }

    [Fact]
    public async Task Overview_ShouldGroupVmsByHypervisor_WithResolvedName()
    {
        await using var context = await CreateContextAsync();
        await SeedAsync(context);

        var result = await OverviewHandler(context).Handle(
            new GetVirtualizationOverviewQuery(), CancellationToken.None);

        result.ByHypervisor.Should().ContainSingle();
        var group = result.ByHypervisor.Single();
        group.Name.Should().Be("esx-01", "o nome do vínculo é resolvido no read-model");
        group.VirtualMachineCount.Should().Be(2, "vm-a e vm-b rodam em esx-01");
        result.ByHypervisor.Should().NotContain(g => g.Name == "srv-01",
            "hypervisor sem VMs não aparece no grupo");
    }

    [Fact]
    public async Task Overview_ShouldGroupVmsByVCenter_WithResolvedName()
    {
        await using var context = await CreateContextAsync();
        await SeedAsync(context);

        var result = await OverviewHandler(context).Handle(
            new GetVirtualizationOverviewQuery(), CancellationToken.None);

        result.ByVCenter.Should().ContainSingle();
        var group = result.ByVCenter.Single();
        group.Name.Should().Be("vcenter-01");
        group.VirtualMachineCount.Should().Be(2, "vm-a e vm-c são gerenciadas");
    }

    [Fact]
    public async Task Overview_ShouldListVmsWithResolvedLinkNames()
    {
        await using var context = await CreateContextAsync();
        await SeedAsync(context);

        var result = await OverviewHandler(context).Handle(
            new GetVirtualizationOverviewQuery(), CancellationToken.None);

        result.VirtualMachines.Select(vm => vm.Name)
            .Should().BeEquivalentTo(["vm-a", "vm-b", "vm-c", "vm-o"],
                "a listagem só traz VMs — devices comuns não entram");

        var vmA = result.VirtualMachines.Single(vm => vm.Name == "vm-a");
        vmA.HypervisorName.Should().Be("esx-01");
        vmA.VCenterName.Should().Be("vcenter-01");
        vmA.IsOrphan.Should().BeFalse();
        vmA.Ip.Should().Be("10.0.10.11");

        var vmB = result.VirtualMachines.Single(vm => vm.Name == "vm-b");
        vmB.HypervisorName.Should().Be("esx-01");
        vmB.VCenterName.Should().BeNull("vm-b não tem vCenter");
        vmB.IsOrphan.Should().BeFalse();

        var orphan = result.VirtualMachines.Single(vm => vm.Name == "vm-o");
        orphan.HypervisorId.Should().BeNull();
        orphan.VCenterId.Should().BeNull();
        orphan.IsOrphan.Should().BeTrue("vm-o não tem nenhum vínculo");
    }

    [Fact]
    public async Task Overview_WithoutSeed_ShouldReturnEmptyOverview()
    {
        await using var context = await CreateContextAsync();

        var result = await OverviewHandler(context).Handle(
            new GetVirtualizationOverviewQuery(), CancellationToken.None);

        result.TotalVirtualMachines.Should().Be(0);
        result.TotalVCenters.Should().Be(0);
        result.OrphanVirtualMachines.Should().Be(0);
        result.ByHypervisor.Should().BeEmpty();
        result.ByVCenter.Should().BeEmpty();
        result.VirtualMachines.Should().BeEmpty();
    }
}
