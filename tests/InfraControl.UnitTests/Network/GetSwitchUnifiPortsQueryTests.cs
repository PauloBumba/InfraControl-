using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Fase 07a: GetSwitchUnifiPortsQuery — resolve MAC via
/// ExternalDeviceReference e devolve port_table ao vivo; sem referência →
/// lista vazia; device não switch → 404; device fora do controller → vazio.
/// </summary>
public sealed class GetSwitchUnifiPortsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeUnifiApiClient _unifi = new();

    public GetSwitchUnifiPortsQueryTests()
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

    private GetSwitchUnifiPortsQueryHandler Handler(AppDbContext context)
        => new(
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            new ExternalDeviceReferenceRepository(context),
            _unifi);

    [Fact]
    public async Task WithUnifiReference_ShouldReturnLivePorts()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _unifi.Ports =
        [
            new UnifiSwitchPortDto(1, "Port 1", true, "1G", true, 10, true, "delivering", "twisted_pair", null),
            new UnifiSwitchPortDto(2, "Port 2", false, "1G", true, 10, false, "none", "twisted_pair", "112233445566"),
        ];

        var result = await Handler(context).Handle(
            new GetSwitchUnifiPortsQuery(switchId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Port 1");
        result[0].LinkUp.Should().BeTrue();
        result[0].PoeStatus.Should().Be("delivering");
        result[1].ClientMac.Should().Be("112233445566");
        _unifi.LastMac.Should().Be("aabbccddeeff");
    }

    [Fact]
    public async Task WithoutUnifiReference_ShouldReturnEmpty()
    {
        var (context, switchId) = await CreateSwitchAsync(withUnifiRef: false);

        var result = await Handler(context).Handle(
            new GetSwitchUnifiPortsQuery(switchId), CancellationToken.None);

        result.Should().BeEmpty();
        _unifi.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DeviceNotFoundInController_ShouldReturnEmptyNotThrow()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _unifi.ThrowNotFound = true;

        var result = await Handler(context).Handle(
            new GetSwitchUnifiPortsQuery(switchId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NonSwitchDevice_ShouldThrowKeyNotFoundPtBr()
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
            new GetSwitchUnifiPortsQuery(device.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    private sealed class FakeUnifiApiClient : IUnifiApiClient
    {
        public IReadOnlyList<UnifiSwitchPortDto> Ports { get; set; } = [];
        public bool ThrowNotFound { get; set; }
        public string? LastMac { get; private set; }
        public int Calls { get; private set; }

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<UnifiSiteDto>> ListSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiSiteDto>>([]);

        public Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiDeviceDto>>([]);

        public Task<IReadOnlyList<UnifiAccessPointDto>> ListAccessPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiAccessPointDto>>([]);

        public Task<UnifiTopologyDto> GetTopologyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UnifiTopologyDto([], []));

        public Task<UnifiPortsSummaryDto> GetPortsSummaryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UnifiPortsSummaryDto(0, 0));

        // Fase 07e (ADR-051): membro novo na interface. Este fake serve à
        // leitura — falhar alto se algum teste chegar a escrever porta.
        public Task SetPortConfigAsync(
            string deviceMac,
            int portIndex,
            bool? disabled,
            int? portVlan,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException(
                "Fake não prevê escrita de porta (ADR-051).");

        public Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
            string deviceMac, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMac = deviceMac;
            if (ThrowNotFound)
                throw new KeyNotFoundException("Device não encontrado no controller.");
            return Task.FromResult(Ports);
        }

        public Task<IReadOnlyList<UnifiConnectedClientDto>> ListConnectedClientsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiConnectedClientDto>>([]);

        public Task<IReadOnlyList<UnifiClientTrafficDto>> ListClientTrafficAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiClientTrafficDto>>([]);

        public Task<IReadOnlyList<UnifiDpiAppDto>> ListDpiStatsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiDpiAppDto>>([]);

        public Task<IReadOnlyList<UnifiSsidDto>> ListSsidsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiSsidDto>>([]);

        public Task<IReadOnlyList<UnifiNetworkDto>> ListNetworksAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiNetworkDto>>([]);

        public Task<string> GetSystemVersionAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult("8.0.0");
    }
}