using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// Fase 07e (ADR-051): SetUnifiPortConfigCommand — guarda type=Switch,
/// exige referência UniFi, exige ao menos um campo, delega gravação ao
/// cliente; sem referência → 409; non-switch → 404; sem campos → 400.
/// </summary>
public sealed class SetUnifiPortConfigCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeUnifiClient _unifi = new();

    public SetUnifiPortConfigCommandTests()
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

    private SetUnifiPortConfigCommandHandler Handler(AppDbContext context)
        => new(
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            new ExternalDeviceReferenceRepository(context),
            _unifi,
            NullLogger<SetUnifiPortConfigCommandHandler>.Instance);

    [Fact]
    public async Task DisablePort_ShouldCallClientAndReturnSuccess()
    {
        var (context, switchId) = await CreateSwitchAsync();

        var result = await Handler(context).Handle(
            new SetUnifiPortConfigCommand(switchId, PortIndex: 5, Disabled: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("desabilitada");
        _unifi.LastMac.Should().Be("aabbccddeeff");
        _unifi.LastPortIndex.Should().Be(5);
        _unifi.LastDisabled.Should().BeTrue();
        _unifi.LastVlan.Should().BeNull();
    }

    [Fact]
    public async Task SetVlan_ShouldCallClientWithVlan()
    {
        var (context, switchId) = await CreateSwitchAsync();

        var result = await Handler(context).Handle(
            new SetUnifiPortConfigCommand(switchId, PortIndex: 3, PortVlan: 99),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("VLAN 99");
        _unifi.LastDisabled.Should().BeNull();
        _unifi.LastVlan.Should().Be(99);
    }

    [Fact]
    public async Task NoFields_ShouldThrowArgumentException()
    {
        var (context, switchId) = await CreateSwitchAsync();

        var act = async () => await Handler(context).Handle(
            new SetUnifiPortConfigCommand(switchId, PortIndex: 1),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*ao menos um campo*");
        _unifi.SetPortCalls.Should().Be(0);
    }

    [Fact]
    public async Task WithoutUnifiReference_ShouldThrowInvalidOperation()
    {
        var (context, switchId) = await CreateSwitchAsync(withUnifiRef: false);

        var act = async () => await Handler(context).Handle(
            new SetUnifiPortConfigCommand(switchId, PortIndex: 1, Disabled: true),
            CancellationToken.None);

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
            new SetUnifiPortConfigCommand(device.Id, PortIndex: 1, Disabled: true),
            CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    private sealed class FakeUnifiClient : IUnifiApiClient
    {
        public string? LastMac { get; private set; }
        public int? LastPortIndex { get; private set; }
        public bool? LastDisabled { get; private set; }
        public int? LastVlan { get; private set; }
        public int SetPortCalls { get; private set; }

        public Task SetPortConfigAsync(
            string deviceMac, int portIndex, bool? disabled, int? portVlan,
            CancellationToken cancellationToken)
        {
            SetPortCalls++;
            LastMac = deviceMac;
            LastPortIndex = portIndex;
            LastDisabled = disabled;
            LastVlan = portVlan;
            return Task.CompletedTask;
        }

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

        public Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
            string deviceMac, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnifiSwitchPortDto>>([]);

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
