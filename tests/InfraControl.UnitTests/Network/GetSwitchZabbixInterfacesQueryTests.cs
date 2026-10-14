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
/// Fase 07b: GetSwitchZabbixInterfacesQuery — resolve referência Zabbix
/// → hostId → ListItemsAsync → filtra itens de interface; sem referência
/// ou Zabbix não configurado → lista vazia; non-switch → 404.
/// </summary>
public sealed class GetSwitchZabbixInterfacesQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeZabbixClient _zabbix = new();

    public GetSwitchZabbixInterfacesQueryTests()
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
            var reference = ExternalDeviceReference.Create(
                ExternalDeviceSource.Zabbix, "10101", device.Id);
            context.ExternalDeviceReferences.Add(reference);
            await context.SaveChangesAsync();
        }

        return (context, device.Id);
    }

    private GetSwitchZabbixInterfacesQueryHandler Handler(AppDbContext context)
        => new(
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            new ExternalDeviceReferenceRepository(context),
            new FakeZabbixHostsRepository(),
            _zabbix);

    [Fact]
    public async Task WithZabbixReference_ShouldFilterInterfaceItems()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = true;
        _zabbix.Items =
        [
            new("1", "10101", "ifOperStatus ge-0/0/0", "ifOperStatus[ge-0/0/0]", 20, true, false),
            new("2", "10101", "ifInOctets ge-0/0/0", "ifInOctets[ge-0/0/0]", 20, true, false),
            new("3", "10101", "CPU load", "system.cpu.util", 20, true, false),
            new("4", "10101", "ICMP ping", "icmpping", 20, true, false),
            new("5", "10101", "ifOperStatus Gi0/1", "ifOperStatus[\"Gi0/1\"]", 20, true, false),
        ];

        var result = await Handler(context).Handle(
            new GetSwitchZabbixInterfacesQuery(switchId), CancellationToken.None);

        // 3 itens de interface (ifOperStatus + ifInOctets em ge-0/0/0, ifOperStatus em Gi0/1);
        // CPU e ICMP filtrados.
        result.Should().HaveCount(3);
        result.Select(i => i.InterfaceName).Should().Contain("ge-0/0/0");
        result.Select(i => i.InterfaceName).Should().Contain("Gi0/1");
        result.Should().NotContain(i => i.Key.Contains("cpu"));
        result.Should().NotContain(i => i.Key.Contains("icmpping"));
    }

    [Fact]
    public async Task WithoutZabbixReference_ShouldReturnEmpty()
    {
        var (context, switchId) = await CreateSwitchAsync(withZabbixRef: false);
        _zabbix.IsConfigured = true;

        var result = await Handler(context).Handle(
            new GetSwitchZabbixInterfacesQuery(switchId), CancellationToken.None);

        result.Should().BeEmpty();
        _zabbix.ListItemsCalls.Should().Be(0);
    }

    [Fact]
    public async Task ZabbixNotConfigured_ShouldReturnEmpty()
    {
        var (context, switchId) = await CreateSwitchAsync();
        _zabbix.IsConfigured = false;

        var result = await Handler(context).Handle(
            new GetSwitchZabbixInterfacesQuery(switchId), CancellationToken.None);

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
            new GetSwitchZabbixInterfacesQuery(device.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    private sealed class FakeZabbixClient : IZabbixClient
    {
        public bool IsConfigured { get; set; }
        public int ListItemsCalls { get; private set; }
        public IReadOnlyList<ZabbixItemDto> Items { get; set; } = [];

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

        public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
            string itemId, DateTime fromUtc, DateTime toUtc, int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ZabbixHistoryValueDto>>([]);
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
