using InfraControl.Application.Network.SwitchPorts.Commands;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.SwitchPortVlans.Commands;
using InfraControl.Application.Network.SwitchPortVlans.Queries;
using InfraControl.Application.Network.Vlans.Commands;
using InfraControl.Application.Network.Vlans.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T004: handlers da junção SwitchPort N:N Vlan em Sqlite. Guards reusados
/// dos slices F3-T002/F3-T003; exclusões com vínculo bloqueadas com 409.
/// </summary>
public sealed class SwitchPortVlanCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SwitchPortVlanCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
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

    private static async Task<Guid> SeedSwitchAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-01", DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static async Task<Guid> SeedPortAsync(AppDbContext context, string name = "Gi1/0/1")
    {
        var switchId = await SeedSwitchAsync(context);
        return await SeedPortOnSwitchAsync(context, switchId, name);
    }

    private static async Task<Guid> SeedPortOnSwitchAsync(
        AppDbContext context,
        Guid switchId,
        string name)
    {
        var port = SwitchPort.Create(switchId, name);
        context.SwitchPorts.Add(port);
        await context.SaveChangesAsync();
        return port.Id;
    }

    private static async Task<Guid> SeedVlanAsync(
        AppDbContext context,
        string name = "Radiologia",
        int number = 10)
    {
        var vlan = Vlan.Create(name, number);
        context.Vlans.Add(vlan);
        await context.SaveChangesAsync();
        return vlan.Id;
    }

    private static async Task SeedLinkAsync(AppDbContext context, Guid portId, Guid vlanId)
    {
        context.SwitchPortVlans.Add(SwitchPortVlan.Create(portId, vlanId));
        await context.SaveChangesAsync();
    }

    private static AssignVlanToSwitchPortCommandHandler AssignHandler(AppDbContext context)
        => new(
            new SwitchPortVlanRepository(context),
            new GetSwitchPortQueryHandler(new SwitchPortRepository(context)),
            new GetVlanQueryHandler(new VlanRepository(context)),
            NullLogger<AssignVlanToSwitchPortCommandHandler>.Instance);

    private static UnassignVlanFromSwitchPortCommandHandler UnassignHandler(AppDbContext context)
        => new(
            new SwitchPortVlanRepository(context),
            new GetSwitchPortQueryHandler(new SwitchPortRepository(context)),
            new GetVlanQueryHandler(new VlanRepository(context)),
            NullLogger<UnassignVlanFromSwitchPortCommandHandler>.Instance);

    private static GetSwitchPortVlansQueryHandler PortVlansHandler(AppDbContext context)
        => new(
            new SwitchPortVlanRepository(context),
            new VlanRepository(context),
            new GetSwitchPortQueryHandler(new SwitchPortRepository(context)));

    private static GetVlanSwitchPortsQueryHandler VlanPortsHandler(AppDbContext context)
        => new(
            new SwitchPortVlanRepository(context),
            new SwitchPortRepository(context),
            new GetVlanQueryHandler(new VlanRepository(context)));

    private static DeleteSwitchPortCommandHandler DeletePortHandler(AppDbContext context)
        => new(
            new SwitchPortRepository(context),
            new SwitchPortVlanRepository(context),
            NullLogger<DeleteSwitchPortCommandHandler>.Instance);

    private static DeleteVlanCommandHandler DeleteVlanHandler(AppDbContext context)
        => new(
            new VlanRepository(context),
            new SwitchPortVlanRepository(context),
            NullLogger<DeleteVlanCommandHandler>.Instance);

    // ── Assign ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_Valid_ShouldReturnVlanDto()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context, "Radiologia", 10);

        var result = await AssignHandler(context).Handle(
            new AssignVlanToSwitchPortCommand(portId, vlanId), CancellationToken.None);

        result.Id.Should().Be(vlanId);
        result.Name.Should().Be("Radiologia");
        result.VlanNumber.Should().Be(10);
    }

    [Fact]
    public async Task Assign_UnknownPort_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownPortId = Guid.NewGuid();
        var vlanId = await SeedVlanAsync(context);

        var act = () => AssignHandler(context).Handle(
            new AssignVlanToSwitchPortCommand(unknownPortId, vlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownPortId}' não encontrado.");
    }

    [Fact]
    public async Task Assign_UnknownVlan_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var unknownVlanId = Guid.NewGuid();

        var act = () => AssignHandler(context).Handle(
            new AssignVlanToSwitchPortCommand(portId, unknownVlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownVlanId}' não encontrada.");
    }

    [Fact]
    public async Task Assign_Duplicate_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context);
        await SeedLinkAsync(context, portId, vlanId);

        var act = () => AssignHandler(context).Handle(
            new AssignVlanToSwitchPortCommand(portId, vlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("A porta já está vinculada a esta VLAN.");
    }

    // ── Unassign ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unassign_Valid_ShouldRemoveLink()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context);
        await SeedLinkAsync(context, portId, vlanId);

        await UnassignHandler(context).Handle(
            new UnassignVlanFromSwitchPortCommand(portId, vlanId), CancellationToken.None);

        var vlans = await PortVlansHandler(context).Handle(
            new GetSwitchPortVlansQuery(portId), CancellationToken.None);
        vlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Unassign_UnknownPort_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownPortId = Guid.NewGuid();
        var vlanId = await SeedVlanAsync(context);

        var act = () => UnassignHandler(context).Handle(
            new UnassignVlanFromSwitchPortCommand(unknownPortId, vlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownPortId}' não encontrado.");
    }

    [Fact]
    public async Task Unassign_UnknownVlan_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var unknownVlanId = Guid.NewGuid();

        var act = () => UnassignHandler(context).Handle(
            new UnassignVlanFromSwitchPortCommand(portId, unknownVlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownVlanId}' não encontrada.");
    }

    [Fact]
    public async Task Unassign_MissingLink_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context);

        var act = () => UnassignHandler(context).Handle(
            new UnassignVlanFromSwitchPortCommand(portId, vlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da porta '{portId}' com a VLAN '{vlanId}' não encontrado.");
    }

    // ── VLANs da porta ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetVlansOfPort_ShouldReturnOrderedByNumber()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        foreach (var (name, number) in new[] { ("Cameras", 20), ("Default", 1), ("Unifi", 100) })
        {
            var vlanId = await SeedVlanAsync(context, name, number);
            await SeedLinkAsync(context, portId, vlanId);
        }

        var result = await PortVlansHandler(context).Handle(
            new GetSwitchPortVlansQuery(portId), CancellationToken.None);

        result.Select(vlan => vlan.VlanNumber).Should().Equal(
            new[] { 1, 20, 100 },
            "ordenação por número 802.1Q");
        result.Should().AllSatisfy(vlan => vlan.Id.Should().NotBe(Guid.Empty));
    }

    [Fact]
    public async Task GetVlansOfPort_UnknownPort_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownPortId = Guid.NewGuid();

        var act = () => PortVlansHandler(context).Handle(
            new GetSwitchPortVlansQuery(unknownPortId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownPortId}' não encontrado.");
    }

    // ── Portas da VLAN ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPortsOfVlan_ShouldReturnOrderedByName()
    {
        await using var context = await CreateContextAsync();
        var vlanId = await SeedVlanAsync(context);
        var switchId = await SeedSwitchAsync(context);
        foreach (var name in new[] { "Gi1/0/2", "Gi1/0/1", "Gi1/0/10" })
        {
            var portId = await SeedPortOnSwitchAsync(context, switchId, name);
            await SeedLinkAsync(context, portId, vlanId);
        }

        var result = await VlanPortsHandler(context).Handle(
            new GetVlanSwitchPortsQuery(vlanId), CancellationToken.None);

        result.Select(port => port.Name).Should().Equal(
            new[] { "Gi1/0/1", "Gi1/0/10", "Gi1/0/2" },
            "ordenação lexicográfica por nome");
        result.Should().AllSatisfy(port => port.DeviceId.Should().Be(switchId));
    }

    [Fact]
    public async Task GetPortsOfVlan_UnknownVlan_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownVlanId = Guid.NewGuid();

        var act = () => VlanPortsHandler(context).Handle(
            new GetVlanSwitchPortsQuery(unknownVlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownVlanId}' não encontrada.");
    }

    // ── Bloqueios de exclusão ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteVlan_WithLinkedPorts_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context);
        await SeedLinkAsync(context, portId, vlanId);

        var act = () => DeleteVlanHandler(context).Handle(
            new DeleteVlanCommand(vlanId), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a VLAN: existem portas vinculadas.");
    }

    [Fact]
    public async Task DeleteVlan_WithoutLinks_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var vlanId = await SeedVlanAsync(context);

        await DeleteVlanHandler(context).Handle(
            new DeleteVlanCommand(vlanId), CancellationToken.None);

        context.Vlans.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSwitchPort_WithLinkedVlans_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var portId = await SeedPortAsync(context);
        var vlanId = await SeedVlanAsync(context);
        await SeedLinkAsync(context, portId, vlanId);

        var act = () => DeletePortHandler(context).Handle(
            new DeleteSwitchPortCommand(portId), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a porta: existem VLANs vinculadas.");
    }
}
