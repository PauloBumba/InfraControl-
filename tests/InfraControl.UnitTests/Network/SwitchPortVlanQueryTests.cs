using FluentAssertions;
using InfraControl.Application.Network.SwitchPortVlans.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// ADR-030 §2 — GET /network/switch-port-vlans/{id}: recuperação pontual da
/// LINHA da junção N:N (as listas dos lados continuam existindo); inexistente
/// = 404 pt-BR.
/// </summary>
public sealed class SwitchPortVlanQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SwitchPortVlanQueryTests()
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

    private static async Task<SwitchPortVlan> SeedLinkAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-01", DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var port = SwitchPort.Create(device.Id, "Gi1/0/1");
        context.SwitchPorts.Add(port);

        var vlan = Vlan.Create("Radiologia", 10);
        context.Vlans.Add(vlan);
        await context.SaveChangesAsync();

        var link = SwitchPortVlan.Create(port.Id, vlan.Id);
        context.SwitchPortVlans.Add(link);
        await context.SaveChangesAsync();
        return link;
    }

    private static GetSwitchPortVlanQueryHandler Handler(AppDbContext context)
        => new(new SwitchPortVlanRepository(context));

    [Fact]
    public async Task GetSwitchPortVlan_KnownLink_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var link = await SeedLinkAsync(context);

        var dto = await Handler(context).Handle(
            new GetSwitchPortVlanQuery(link.Id), CancellationToken.None);

        dto.Id.Should().Be(link.Id);
        dto.SwitchPortId.Should().Be(link.SwitchPortId);
        dto.VlanId.Should().Be(link.VlanId);
    }

    [Fact]
    public async Task GetSwitchPortVlan_Unknown_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new GetSwitchPortVlanQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vinculação switch-port/vlan '{unknownId}' não encontrada.");
    }
}
