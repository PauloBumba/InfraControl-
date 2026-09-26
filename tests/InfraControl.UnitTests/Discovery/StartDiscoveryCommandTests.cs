using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Repositories;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T003: gatilho manual de discovery (decisões do usuário: POST
/// incluído; ping falho = achado negativo com run Completed, não run
/// Fail). Só erro de infraestrutura (ex.: exceção do probe) falha o run.
/// </summary>
public sealed class StartDiscoveryCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public StartDiscoveryCommandTests()
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

    private sealed class FakeIcmpClient(IcmpReply reply) : IIcmpClient
    {
        public Task<IcmpReply> SendAsync(
            string target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(reply);
    }

    private sealed class ThrowingIcmpClient : IIcmpClient
    {
        public Task<IcmpReply> SendAsync(
            string target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new TimeoutException("boom");
    }

    private static StartDiscoveryCommandHandler Handler(
        AppDbContext context,
        IIcmpClient icmpClient)
        => new(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>([new IcmpDiscoveryProvider(icmpClient)]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context));

    private static async Task<Guid> SeedDeviceAsync(
        AppDbContext context, string? ip = "192.0.2.40")
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-disc", DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    [Fact]
    public void Command_ShouldRequireMonitoringManage()
        => new StartDiscoveryCommand(Guid.NewGuid(), "Icmp")
            .RequiredPermissions.Should()
            .Equal(Permission.MonitoringManage.ToString());

    [Fact]
    public async Task Start_Success_ShouldCompleteRunAndPersistHostResult()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var dto = await Handler(context, new FakeIcmpClient(new IcmpReply(true, 5)))
            .Handle(new StartDiscoveryCommand(deviceId, "Icmp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        dto.ErrorMessage.Should().BeNull();

        var run = context.DiscoveryRuns.Single(r => r.Id == dto.Id);
        run.Status.Should().Be(DiscoveryRunStatus.Completed);
        run.FinishedAt.Should().NotBeNull();

        var result = context.DiscoveryResults.Single();
        result.DiscoveryRunId.Should().Be(dto.Id);
        result.Kind.Should().Be(DiscoveryResultKind.Host);
        result.DeviceId.Should().Be(deviceId);
        result.Summary.Should().Contain("acessível");
        result.Values.Should().NotBeNull();
        result.Values!["reachable"].Should().Be("true");
        result.Values!["roundtrip_ms"].Should().Be("5");
    }

    [Fact]
    public async Task Start_Unreachable_ShouldCompleteRunWithNegativeFinding()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var dto = await Handler(context, new FakeIcmpClient(new IcmpReply(false, 0, "sem rota")))
            .Handle(new StartDiscoveryCommand(deviceId, "icmp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Summary.Should().Contain("inacessível");
        result.Values!["reachable"].Should().Be("false");
        context.DiscoveryRuns.Single().Status.Should().Be(DiscoveryRunStatus.Completed);
    }

    [Fact]
    public async Task Start_Snmp_ShouldPersistSystemGroupResult()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.70");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.1.1.0", "MikroTik RouterOS"),
            new("1.3.6.1.2.1.1.5.0", "rb-core-01")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new SnmpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context));

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Snmp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.Host);
        result.Summary.Should().Contain("rb-core-01");
        result.Values!["sys_name"].Should().Be("rb-core-01");
        result.Values!["reachable"].Should().Be("true");
    }

    private sealed class FakeSnmpWalkClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target, string oid, TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(walkFactory(target));
    }

    [Fact]
    public async Task Start_Lldp_ShouldPersistOneNeighborFindingEachWithoutDeviceLink()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.80");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.0.8802.1.1.2.1.4.1.1.7.0.11.1", "sw-core-01"),
            new("1.0.8802.1.1.2.1.4.1.1.6.0.11.1", "Gi0/1"),
            new("1.0.8802.1.1.2.1.4.1.1.7.0.11.2", "sw-access-02"),
            new("1.0.8802.1.1.2.1.4.1.1.5.0.11.2", "Gi1/0/24")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new LldpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context));

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Lldp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var results = context.DiscoveryResults.ToList();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Kind == DiscoveryResultKind.Neighbor);
        results.Should().OnlyContain(r => r.DeviceId == null,
            "vizinho não é o device escaneado — vínculo vem no T010");
        results.Select(r => r.Summary).Should().BeEquivalentTo(
            "sw-core-01 — Gi0/1", "sw-access-02 — Gi1/0/24");
    }

    [Fact]
    public async Task Start_LldpEmptyWalk_ShouldRegisterNoNeighborsFinding()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.80");

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new LldpDiscoveryProvider(new FakeSnmpWalkClient(_ => []))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context));

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "lldp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        var result = context.DiscoveryResults.Single();
        result.Summary.Should().Be("Nenhum vizinho LLDP.");
        result.Values!["neighbors"].Should().Be("0");
    }

    [Fact]
    public async Task Start_Cdp_ShouldPersistOneNeighborFindingEach()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.90");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.4.1.9.9.23.1.2.1.1.6.11.1", "sw-cisco-01"),
            new("1.3.6.1.4.1.9.9.23.1.2.1.1.7.11.1", "GigabitEthernet0/1")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new CdpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context));

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Cdp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.Neighbor);
        result.Summary.Should().Be("sw-cisco-01 — GigabitEthernet0/1");
        result.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Start_WithoutIp_ShouldThrowPtBrAndCreateNoRun()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: null);

        var act = () => Handler(context, new FakeIcmpClient(new IcmpReply(true, 1)))
            .Handle(new StartDiscoveryCommand(deviceId, "Icmp"), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*sem IP*");
        context.DiscoveryRuns.Should().BeEmpty();
        context.DiscoveryResults.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_UnknownDevice_ShouldThrowNotFound()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => Handler(context, new FakeIcmpClient(new IcmpReply(true, 1)))
            .Handle(new StartDiscoveryCommand(unknownId, "Icmp"), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Start_UnknownProvider_ShouldThrowPtBrAndCreateNoRun()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var act = () => Handler(context, new FakeIcmpClient(new IcmpReply(true, 1)))
            .Handle(new StartDiscoveryCommand(deviceId, "Foo"), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Provider de discovery 'Foo'*");
        context.DiscoveryRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_ProviderThrows_ShouldFailRunAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var dto = await Handler(context, new ThrowingIcmpClient())
            .Handle(new StartDiscoveryCommand(deviceId, "Icmp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Failed));
        dto.ErrorMessage.Should().Be("boom");

        context.DiscoveryRuns.Single().Status.Should().Be(DiscoveryRunStatus.Failed);
        context.DiscoveryResults.Should().BeEmpty();
    }
}
