using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.SwitchPorts.Commands;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Fase 07c: SyncSwitchPortsFromUnifiCommand — cria portas novas, atualiza
/// LinkUp das existentes, preserva manuais ausentes do controller; sem
/// referência UniFi → 409; non-switch → 404.
/// </summary>
public sealed class SyncSwitchPortsFromUnifiCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeUnifiApiClient _unifi = new();

    public SyncSwitchPortsFromUnifiCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, Guid SwitchId)> CreateSwitchAsync(
        bool withUnifiRef = true)
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var location = Location.Create("DC A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-core-01", DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        if (withUnifiRef)
        {
            var reference = ExternalDeviceReference.Create(
                ExternalDeviceSource.UniFi, "aabbccddeeff", device.Id);
            context.ExternalDeviceReferences.Add(reference);
            await context.SaveChangesAsync();
        }

        return (context, device.Id);
    }

    private SyncSwitchPortsFromUnifiCommandHandler Handler(AppDbContext context)
        => new(
            new SwitchPortRepository(context),
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            new ExternalDeviceReferenceRepository(context),
            _unifi,
            NullLogger<SyncSwitchPortsFromUnifiCommandHandler>.Instance);

    [Fact]
    public async Task NewPorts_ShouldBeCreated()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _unifi.Ports =
        [
            new UnifiSwitchPortDto(1, "Port 1", true, "1G", true, 10, false, "none", "twisted_pair", null),
            new UnifiSwitchPortDto(2, "Port 2", false, "1G", true, 10, false, "none", "twisted_pair", null),
        ];

        var result = await Handler(context).Handle(
            new SyncSwitchPortsFromUnifiCommand(switchId), CancellationToken.None);

        result.Created.Should().Be(2);
        result.Updated.Should().Be(0);
        result.TotalFromController.Should().Be(2);
        result.TotalAfterSync.Should().Be(2);

        var ports = await context.SwitchPorts
            .Where(p => p.DeviceId == switchId)
            .OrderBy(p => p.Name)
            .ToListAsync();
        ports.Should().HaveCount(2);
        ports[0].LinkUp.Should().BeTrue();
        ports[1].LinkUp.Should().BeFalse();
    }

    [Fact]
    public async Task ExistingPort_LinkUpChanged_ShouldUpdate()
    {
        var (context, switchId) = await CreateSwitchAsync();
        var existing = SwitchPort.Create(switchId, "Port 1", null);
        context.SwitchPorts.Add(existing);
        await context.SaveChangesAsync();

        _unifi.Ports =
        [
            new UnifiSwitchPortDto(1, "Port 1", false, "1G", true, 10, false, "none", "twisted_pair", null),
        ];

        var result = await Handler(context).Handle(
            new SyncSwitchPortsFromUnifiCommand(switchId), CancellationToken.None);

        result.Created.Should().Be(0);
        result.Updated.Should().Be(1);

        var port = await context.SwitchPorts.SingleAsync(p => p.DeviceId == switchId);
        port.LinkUp.Should().BeFalse();
    }

    [Fact]
    public async Task ManualPortAbsentFromController_ShouldBePreserved()
    {
        var (context, switchId) = await CreateSwitchAsync();
        var manual = SwitchPort.Create(switchId, "Uplink-to-core", "manual");
        context.SwitchPorts.Add(manual);
        await context.SaveChangesAsync();

        _unifi.Ports =
        [
            new UnifiSwitchPortDto(1, "Port 1", true, "1G", true, 10, false, "none", "twisted_pair", null),
        ];

        var result = await Handler(context).Handle(
            new SyncSwitchPortsFromUnifiCommand(switchId), CancellationToken.None);

        result.Created.Should().Be(1);
        result.TotalAfterSync.Should().Be(2);

        var ports = await context.SwitchPorts
            .Where(p => p.DeviceId == switchId)
            .ToListAsync();
        ports.Should().Contain(p => p.Name == "Uplink-to-core");
        ports.Should().Contain(p => p.Name == "Port 1");
    }

    [Fact]
    public async Task WithoutUnifiReference_ShouldThrowInvalidOperation()
    {
        var (context, switchId) = await CreateSwitchAsync(withUnifiRef: false);

        var act = async () => await Handler(context).Handle(
            new SyncSwitchPortsFromUnifiCommand(switchId), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*referência UniFi*");
    }

    [Fact]
    public async Task NonSwitchDevice_ShouldThrowKeyNotFound()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var location = Location.Create("DC A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(location.Id, "pr-01", DeviceType.Printer);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var act = async () => await Handler(context).Handle(
            new SyncSwitchPortsFromUnifiCommand(device.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    private sealed class FakeUnifiApiClient : IUnifiApiClient
    {
        public IReadOnlyList<UnifiSwitchPortDto> Ports { get; set; } = [];

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<IReadOnlyList<UnifiSiteDto>> ListSitesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiSiteDto>>([]);

        public Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiDeviceDto>>([]);

        public Task<IReadOnlyList<UnifiAccessPointDto>> ListAccessPointsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiAccessPointDto>>([]);

        public Task<UnifiTopologyDto> GetTopologyAsync(CancellationToken cancellationToken)
            => Task.FromResult(new UnifiTopologyDto([], []));

        public Task<UnifiPortsSummaryDto> GetPortsSummaryAsync(CancellationToken cancellationToken)
            => Task.FromResult(new UnifiPortsSummaryDto(0, 0));

        // Fase 07e (ADR-051): membro novo na interface. Este fake serve ao
        // sync de leitura — falhar alto se algum teste chegar a escrever porta.
        public Task SetPortConfigAsync(
            string deviceMac,
            int portIndex,
            bool? disabled,
            int? portVlan,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException(
                "Fake não prevê escrita de porta (ADR-051).");

        public Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
            string deviceMac, CancellationToken cancellationToken)
            => Task.FromResult(Ports);

        public Task<IReadOnlyList<UnifiConnectedClientDto>> ListConnectedClientsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiConnectedClientDto>>([]);

        public Task<IReadOnlyList<UnifiClientTrafficDto>> ListClientTrafficAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiClientTrafficDto>>([]);

        public Task<IReadOnlyList<UnifiDpiAppDto>> ListDpiStatsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiDpiAppDto>>([]);

        public Task<IReadOnlyList<UnifiSsidDto>> ListSsidsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiSsidDto>>([]);

        public Task<IReadOnlyList<UnifiNetworkDto>> ListNetworksAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiNetworkDto>>([]);

        public Task<string> GetSystemVersionAsync(CancellationToken cancellationToken)
            => Task.FromResult("8.0.0");
    }
}
