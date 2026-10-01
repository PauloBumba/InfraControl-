using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

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
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

    private static NeighborConnectionImporter Importer(AppDbContext context)
        => new(
            new NetworkInterfaceRepository(context),
            new ConnectionRepository(context),
            NullLogger<NeighborConnectionImporter>.Instance);

    private static async Task<Guid> SeedDeviceAsync(
        AppDbContext context,
        string? ip = "192.0.2.40",
        string name = "sw-disc",
        string? mac = null)
    {
        var location = await context.Locations.FirstOrDefaultAsync();
        if (location is null)
        {
            location = Location.Create("Data Center A");
            context.Locations.Add(location);
            await context.SaveChangesAsync();
        }

        var device = Device.Create(
            location.Id, name, DeviceType.Switch, ip: ip, mac: mac);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    [Fact]
    public void Command_ShouldRequireOperatorOrAdmin()
        => new StartDiscoveryCommand(Guid.NewGuid(), "Icmp")
            .RequiredRoles.Should()
            .BeEquivalentTo(RoleAccess.Operate);

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
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

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
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

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
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

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
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Cdp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.Neighbor);
        result.Summary.Should().Be("sw-cisco-01 — GigabitEthernet0/1");
        result.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Start_Arp_ShouldPersistArpEntryFindingsWithoutDeviceLink()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.4.22.1.3.11.192.0.2.10", "192.0.2.10"),
            new("1.3.6.1.2.1.4.22.1.2.11.192.0.2.10", "aa:bb:cc:dd:ee:01")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new ArpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Arp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.ArpEntry);
        result.Summary.Should().Be("192.0.2.10 — aa:bb:cc:dd:ee:01");
        result.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Start_Dhcp_ShouldPersistDhcpLeaseFindingsWithoutDeviceLink()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.4.1.9.9.380.1.4.1.2.10.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01"),
            new("1.3.6.1.4.1.9.9.380.1.4.1.4.10.170.187.204.221.238.1", "192.0.2.10")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new DhcpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Dhcp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.DhcpLease);
        result.Summary.Should().Be("192.0.2.10 — aa:bb:cc:dd:ee:01");
        result.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Start_Mac_ShouldPersistMacTableFindingsWithoutDeviceLink()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.17.4.3.1.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01"),
            new("1.3.6.1.2.1.17.4.3.2.170.187.204.221.238.1", "11"),
            new("1.3.6.1.2.1.17.4.3.3.170.187.204.221.238.1", "3")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new MacTableDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Mac"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.MacTableEntry);
        result.Summary.Should().Be("aa:bb:cc:dd:ee:01 — porta 11");
        result.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Start_Arp_FindingWithMatchingIp_ShouldLinkByIpWithPriorityOverMac()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");
        var byIp = await SeedDeviceAsync(context, ip: "192.0.2.10", name: "pc-01");
        _ = await SeedDeviceAsync(context, ip: null, name: "pc-02", mac: "aa:bb:cc:dd:ee:01");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.4.22.1.3.11.192.0.2.10", "192.0.2.10"),
            new("1.3.6.1.2.1.4.22.1.2.11.192.0.2.10", "aa:bb:cc:dd:ee:01")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new ArpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Arp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        // ip vence mac: o achado casa pc-01 (ip), não pc-02 (mac).
        var result = context.DiscoveryResults.Single();
        result.DeviceId.Should().Be(byIp);
        result.Values!.ContainsKey("new_device").Should().BeFalse();
    }

    [Fact]
    public async Task Start_Arp_FindingWithMatchingMac_ShouldLinkByMac()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");
        var byMac = await SeedDeviceAsync(
            context, ip: null, name: "pc-03", mac: "AA:BB:CC:DD:EE:09");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.4.22.1.3.11.192.0.2.77", "192.0.2.77"),
            new("1.3.6.1.2.1.4.22.1.2.11.192.0.2.77", "aa:bb:cc:dd:ee:09")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new ArpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Arp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.DeviceId.Should().Be(byMac);
        result.Values!.ContainsKey("new_device").Should().BeFalse();
    }

    [Fact]
    public async Task Start_Cdp_FindingWithMatchingName_ShouldLinkByName()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");
        var byName = await SeedDeviceAsync(context, ip: null, name: "sw-cdp-01");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.4.1.9.9.23.1.2.1.1.6.11.1", "sw-cdp-01"),
            new("1.3.6.1.4.1.9.9.23.1.2.1.1.7.11.1", "GigabitEthernet0/1")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new CdpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Cdp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.DeviceId.Should().Be(byName);
        result.Values!.ContainsKey("new_device").Should().BeFalse();
    }

    [Fact]
    public async Task Start_Dhcp_FindingWithMatchingHostname_ShouldLinkByName()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");
        var byName = await SeedDeviceAsync(context, ip: null, name: "pc-01");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.4.1.9.9.380.1.4.1.2.10.170.187.204.221.238.1", "aa:bb:cc:dd:ee:01"),
            new("1.3.6.1.4.1.9.9.380.1.4.1.4.10.170.187.204.221.238.1", "192.0.2.10"),
            new("1.3.6.1.4.1.9.9.380.1.4.1.8.10.170.187.204.221.238.1", "pc-01")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new DhcpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Dhcp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.DeviceId.Should().Be(byName);
        result.Values!.ContainsKey("new_device").Should().BeFalse();
    }

    [Fact]
    public async Task Start_Arp_UnmatchedFinding_ShouldFlagAsNewDevice()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");

        IReadOnlyList<SnmpVariable> Walk(string _) =>
        [
            new("1.3.6.1.2.1.4.22.1.3.11.192.0.2.50", "192.0.2.50"),
            new("1.3.6.1.2.1.4.22.1.2.11.192.0.2.50", "aa:bb:cc:dd:ee:77")
        ];

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new ArpDiscoveryProvider(new FakeSnmpWalkClient(Walk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Arp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.DeviceId.Should().BeNull();
        result.Values!["new_device"].Should().Be("true");
    }

    [Fact]
    public async Task Start_Arp_NegativeFinding_ShouldNotFlagNewDevice()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context, ip: "192.0.2.1");

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new ArpDiscoveryProvider(new FakeSnmpWalkClient(_ => []))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(deviceId, "Arp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var result = context.DiscoveryResults.Single();
        result.Summary.Should().Be("Nenhuma entrada ARP.");
        result.Values!.ContainsKey("new_device").Should().BeFalse();
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

    // ── F3-T005b: ingestão de Connections a partir de vizinhos ────────────

    private sealed class OidAwareWalkClient(
        Func<string, IReadOnlyList<SnmpVariable>> walkFactory) : ISnmpClient
    {
        public Task<IReadOnlyList<SnmpVariable>> WalkAsync(
            string target, string oid, TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => Task.FromResult(walkFactory(oid));
    }

    private static async Task<Guid> SeedInterfaceAsync(
        AppDbContext context, Guid deviceId, string name)
    {
        var nic = NetworkInterface.Create(deviceId, name);
        context.NetworkInterfaces.Add(nic);
        await context.SaveChangesAsync();
        return nic.Id;
    }

    // Vizinho LLDP: local_port=11 → ifName "Gi0/11"; sys_name sw-remoto
    // casa o device vizinho (F4-T010) e port_desc "Gi0/24" a interface dele.
    private static IReadOnlyList<SnmpVariable> NeighborWalk(string oid)
        => oid == LldpDiscoveryProvider.IfNameTableOid
            ? [new SnmpVariable($"{LldpDiscoveryProvider.IfNameTableOid}.11", "Gi0/11")]
            :
            [
                new("1.0.8802.1.1.2.1.4.1.1.7.0.11.1", "sw-remoto"),
                new("1.0.8802.1.1.2.1.4.1.1.6.0.11.1", "Gi0/24")
            ];

    private static StartDiscoveryCommandHandler LldpHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new LldpDiscoveryProvider(new OidAwareWalkClient(NeighborWalk))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

    [Fact]
    public async Task Start_Lldp_ShouldCreateConnectionFromNeighborFinding()
    {
        // F3-T005b: gatilho automático no fim do run — achado Neighbor com
        // DeviceId (F4-T010) + local_if_name gera Connection dirigida.
        await using var context = await CreateContextAsync();
        var localId = await SeedDeviceAsync(context, ip: "192.0.2.80", name: "sw-local");
        var localNic = await SeedInterfaceAsync(context, localId, "Gi0/11");
        var remoteId = await SeedDeviceAsync(context, ip: null, name: "sw-remoto");
        var remoteNic = await SeedInterfaceAsync(context, remoteId, "Gi0/24");

        var dto = await LldpHandler(context).Handle(
            new StartDiscoveryCommand(localId, "Lldp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        var connection = context.Connections.Should().ContainSingle().Subject;
        connection.SourceInterfaceId.Should().Be(localNic);
        connection.TargetInterfaceId.Should().Be(remoteNic);
    }

    [Fact]
    public async Task Start_Lldp_NeighborWithoutLocalIfName_ShouldNotCreateConnection()
    {
        // Sem walk do ifName (fake devolve RemTable para todo OID) → sem
        // local_if_name → ingestão pula — run continua Completed.
        await using var context = await CreateContextAsync();
        var localId = await SeedDeviceAsync(context, ip: "192.0.2.80", name: "sw-local");
        _ = await SeedInterfaceAsync(context, localId, "Gi0/11");
        var remoteId = await SeedDeviceAsync(context, ip: null, name: "sw-remoto");
        _ = await SeedInterfaceAsync(context, remoteId, "Gi0/24");

        var handler = new StartDiscoveryCommandHandler(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                [new LldpDiscoveryProvider(new FakeSnmpWalkClient(_ => NeighborWalk("x")))]),
            new DiscoveryRunRepository(context),
            new DiscoveryResultRepository(context),
            Importer(context),
            NullLogger<StartDiscoveryCommandHandler>.Instance);

        var dto = await handler.Handle(
            new StartDiscoveryCommand(localId, "Lldp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        context.Connections.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_Lldp_Rerun_ShouldDedupeConnection()
    {
        await using var context = await CreateContextAsync();
        var localId = await SeedDeviceAsync(context, ip: "192.0.2.80", name: "sw-local");
        _ = await SeedInterfaceAsync(context, localId, "Gi0/11");
        var remoteId = await SeedDeviceAsync(context, ip: null, name: "sw-remoto");
        _ = await SeedInterfaceAsync(context, remoteId, "Gi0/24");

        var handler = LldpHandler(context);
        await handler.Handle(
            new StartDiscoveryCommand(localId, "Lldp"), CancellationToken.None);
        await handler.Handle(
            new StartDiscoveryCommand(localId, "Lldp"), CancellationToken.None);

        context.Connections.Should().ContainSingle(
            "dedupe por par dirigido no fim de cada run");
    }

    [Fact]
    public async Task Start_Lldp_UnknownNeighbor_ShouldNotCreateConnection()
    {
        // Vizinho fora do inventário (DeviceId null → new_device) → pula.
        await using var context = await CreateContextAsync();
        var localId = await SeedDeviceAsync(context, ip: "192.0.2.80", name: "sw-local");
        _ = await SeedInterfaceAsync(context, localId, "Gi0/11");

        var handler = LldpHandler(context);
        var dto = await handler.Handle(
            new StartDiscoveryCommand(localId, "Lldp"), CancellationToken.None);

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        context.DiscoveryResults.Single().Values!["new_device"].Should().Be("true");
        context.Connections.Should().BeEmpty();
    }
}
