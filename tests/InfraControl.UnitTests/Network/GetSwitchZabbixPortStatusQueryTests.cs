using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// Status ao vivo de portas Zabbix (net.if.*): agrupa status/in/out por
/// ifIndex, interpreta ifOperStatus e calcula taxa bytes/s dos dois
/// últimos pontos do histórico em lote.
/// </summary>
public sealed class GetSwitchZabbixPortStatusQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeZabbixClient _zabbix = new();

    public GetSwitchZabbixPortStatusQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, Guid SwitchId)> CreateSwitchAsync(
        bool withZabbixRef = true)
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var location = Location.Create("DC A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-core-01", DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        if (withZabbixRef)
        {
            context.ExternalDeviceReferences.Add(ExternalDeviceReference.Create(
                ExternalDeviceSource.Zabbix, "10101", device.Id));
            await context.SaveChangesAsync();
        }

        return (context, device.Id);
    }

    private GetSwitchZabbixPortStatusQueryHandler Handler(AppDbContext context)
        => new(
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            new ExternalDeviceReferenceRepository(context),
            new FakeZabbixHostsRepository(),
            _zabbix);

    private static ZabbixItemDto Item(
        string id, string key, string name) =>
        new(id, "10101", name, key, 20, true, false);

    [Fact]
    public async Task ShouldGroupStatusAndTrafficByIfIndex()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = true;
        _zabbix.Items =
        [
            Item("1", "net.if.status[ifOperStatus.10]",
                "Interface GigabitEthernet1/0/10(GigabitEthernet1/0/10 Interface): Operational status"),
            Item("2", "net.if.in[ifHCInOctets.10]",
                "Interface GigabitEthernet1/0/10(GigabitEthernet1/0/10 Interface): Inbound"),
            Item("3", "net.if.out[ifHCOutOctets.10]",
                "Interface GigabitEthernet1/0/10(GigabitEthernet1/0/10 Interface): Outbound"),
            Item("4", "net.if.status[ifOperStatus.11]",
                "Interface GigabitEthernet1/0/11(GigabitEthernet1/0/11 Interface): Operational status"),
            Item("5", "net.if.in.errors[ifInErrors.10]",
                "Interface GigabitEthernet1/0/10: In errors"), // não casa prefixo net.if.in[
            Item("6", "system.cpu.util", "CPU"),
            Item("7", "net.if.speed[ifHighSpeed.10]",
                "Interface GigabitEthernet1/0/10(GigabitEthernet1/0/10 Interface): Speed"),
            Item("8", "net.if.duplex[dot3StatsDuplexStatus.10]",
                "Interface GigabitEthernet1/0/10(GigabitEthernet1/0/10 Interface): Duplex"),
        ];
        _zabbix.LastValues =
        [
            new("1", "net.if.status[ifOperStatus.10]", "1", DateTime.UtcNow),
            new("4", "net.if.status[ifOperStatus.11]", "2", DateTime.UtcNow),
            new("7", "net.if.speed[ifHighSpeed.10]", "1000", DateTime.UtcNow),
            new("8", "net.if.duplex[dot3StatsDuplexStatus.10]", "3", DateTime.UtcNow),
        ];
        _zabbix.History =
        [
            // item 2: 1000 → 2000 bytes em 10s = 100 B/s
            new("2", new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc), "2000"),
            new("2", new DateTime(2026, 11, 11, 11, 59, 50, DateTimeKind.Utc), "1000"),
            // item 3: 5000 → 6000 em 10s = 100 B/s
            new("3", new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc), "6000"),
            new("3", new DateTime(2026, 11, 11, 11, 59, 50, DateTimeKind.Utc), "5000"),
        ];

        var result = await Handler(context).Handle(
            new GetSwitchZabbixPortStatusQuery(switchId), CancellationToken.None);

        result.Should().HaveCount(2);
        var port10 = result.Single(p => p.IfIndex == 10);
        port10.InterfaceName.Should().Be("GigabitEthernet1/0/10");
        port10.LinkUp.Should().BeTrue();
        port10.SpeedMbps.Should().Be(1000);
        port10.Duplex.Should().Be("Full");
        port10.InBytesPerSecond.Should().BeApproximately(100, 0.001);
        port10.OutBytesPerSecond.Should().BeApproximately(100, 0.001);

        var port11 = result.Single(p => p.IfIndex == 11);
        port11.LinkUp.Should().BeFalse();
        port11.InBytesPerSecond.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnEmptyWithoutZabbixReference()
    {
        var (context, switchId) = await CreateSwitchAsync(withZabbixRef: false);
        _zabbix.IsConfigured = true;

        var result = await Handler(context).Handle(
            new GetSwitchZabbixPortStatusQuery(switchId), CancellationToken.None);

        result.Should().BeEmpty();
        _zabbix.ListItemsCalls.Should().Be(0);
    }

    [Fact]
    public async Task ShouldReturnEmptyWhenNoInterfaceItems()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = true;
        _zabbix.Items = [Item("1", "system.cpu.util", "CPU")];

        var result = await Handler(context).Handle(
            new GetSwitchZabbixPortStatusQuery(switchId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldReturnNullLinkWhenStatusItemHasNoValue()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = true;
        _zabbix.Items =
        [
            Item("1", "net.if.status[ifOperStatus.3]",
                "Interface port 3: Gigabit Copper(): Operational status"),
        ];

        var result = await Handler(context).Handle(
            new GetSwitchZabbixPortStatusQuery(switchId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].InterfaceName.Should().Be("port 3");
        result[0].LinkUp.Should().BeNull();
    }

    [Fact]
    public async Task ShouldSkipCounterResetAndSinglePoint()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = true;
        _zabbix.Items =
        [
            Item("2", "net.if.in[ifHCInOctets.10]", "Interface Gi1: Inbound"),
        ];
        _zabbix.History =
        [
            // reset: valor novo MENOR que o anterior → sem taxa
            new("2", new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc), "500"),
            new("2", new DateTime(2026, 11, 11, 11, 59, 50, DateTimeKind.Utc), "9000"),
            // item 9: ponto único → sem taxa
            new("9", new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc), "100"),
        ];

        var result = await Handler(context).Handle(
            new GetSwitchZabbixPortStatusQuery(switchId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].InBytesPerSecond.Should().BeNull();
    }

    private sealed class FakeZabbixClient : IZabbixClient
    {
        public bool IsConfigured { get; set; }
        public int ListItemsCalls { get; private set; }
        public IReadOnlyList<ZabbixItemDto> Items { get; set; } = [];
        public IReadOnlyList<ZabbixItemLastValueDto> LastValues { get; set; } = [];
        public IReadOnlyList<ZabbixHistoryValueDto> History { get; set; } = [];

        public Task<string> GetVersionAsync(CancellationToken cancellationToken)
            => Task.FromResult("8.0.0");

        public Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixHostDto>>([]);

        public Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixHostGroupDto>>([]);

        public Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixTemplateDto>>([]);

        public Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
            string hostId, CancellationToken cancellationToken)
        {
            ListItemsCalls++;
            return Task.FromResult(Items);
        }

        public Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixTriggerDto>>([]);

        public Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixProblemDto>>([]);

        public Task<ZabbixEventContextDto?> GetEventContextAsync(
            string eventId, CancellationToken cancellationToken)
            => Task.FromResult<ZabbixEventContextDto?>(null);

        public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
            string itemId, DateTime fromUtc, DateTime toUtc, int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixHistoryValueDto>>([]);

        public Task<IReadOnlyList<ZabbixItemLastValueDto>> GetItemsLastValuesAsync(
            string hostId, CancellationToken cancellationToken)
            => Task.FromResult(LastValues);

        public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryBatchAsync(
            IReadOnlyList<string> itemIds, int history,
            DateTime fromUtc, DateTime toUtc, int limit,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ZabbixHistoryValueDto> filtered =
                History.Where(h => itemIds.Contains(h.ItemId)).ToList();
            return Task.FromResult(filtered);
        }
    }

    private sealed class FakeZabbixHostsRepository : IZabbixHostsRepository
    {
        public Task<ZabbixHost?> GetByHostIdAsync(
            string hostId, CancellationToken cancellationToken)
        {
            if (hostId == "10101")
            {
                var host = ZabbixHost.Create(
                    "10101", "sw-core-01", "SW Core 01",
                    null, null, enabled: true, discovered: true,
                    [], [], DateTime.UtcNow);
                return Task.FromResult<ZabbixHost?>(host);
            }
            return Task.FromResult<ZabbixHost?>(null);
        }

        public Task<IReadOnlyList<ZabbixHost>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixHost>>([]);

        public Task<InfraControl.Application.Common.Interfaces.PagedResult<ZabbixHost>> SearchAsync(
            string? term, bool? enabled, bool? missing, int page, int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new InfraControl.Application.Common.Interfaces.PagedResult<ZabbixHost>(
                [], 0, page, pageSize));

        public Task<ZabbixHostCounts> GetCountsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ZabbixHostCounts(0, 0, 0));

        public Task AddAsync(ZabbixHost host, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
