using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F16-T001/ADR-023: webhook Zabbix → Incident — PROBLEM cria (device
/// opcional, host/ip na descrição), dedup anti-storm por título+device,
/// OK resolve, severidade 0..5 mapeada e validações em pt-BR.
/// </summary>
public sealed class ZabbixIngestTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ZabbixIngestTests()
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

    private static IngestZabbixProblemCommandHandler Handler(AppDbContext context)
        => new(
            new IncidentRepository(context),
            new DeviceRepository(context),
            NullLogger<IngestZabbixProblemCommandHandler>.Instance);

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext context,
        string ip = "192.0.2.71",
        string name = "sw-zabbix")
    {
        var location = Location.Create("Sala Zabbix");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, name, DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    [Fact]
    public async Task Problem_WithMatchingIp_ShouldCreateIncidentLinkedToDevice()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var result = await Handler(context).Handle(
            new IngestZabbixProblemCommand(
                "evt-100", "sw-zabbix", "192.0.2.71", "Ping down", 5, "PROBLEM"),
            CancellationToken.None);

        result.Outcome.Should().Be("created");
        result.IncidentId.Should().NotBeNull();

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().Be(device.Id);
        incident.Title.Should().Be("Ping down");
        incident.Severity.Should().Be(IncidentSeverity.Critical);
        incident.Status.Should().Be(IncidentStatus.Open);
        incident.Description.Should()
            .Contain("evt-100").And.Contain("sw-zabbix").And.Contain("192.0.2.71");
    }

    [Fact]
    public async Task Problem_WithoutKnownDevice_ShouldCreateIncidentWithoutDevice()
    {
        await using var context = await CreateContextAsync();

        var result = await Handler(context).Handle(
            new IngestZabbixProblemCommand(
                "evt-200", "host-desconhecido", "198.51.100.9", "Disco cheio", 3, "PROBLEM"),
            CancellationToken.None);

        result.Outcome.Should().Be("created");

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().BeNull();
        incident.Severity.Should().Be(IncidentSeverity.High);
        incident.Description.Should()
            .Contain("host-desconhecido").And.Contain("198.51.100.9");
    }

    [Fact]
    public async Task Problem_IpUnknown_HostFallback_ShouldMatchByName()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var result = await Handler(context).Handle(
            new IngestZabbixProblemCommand(
                "evt-300", "sw-zabbix", "198.51.100.7", "Link down", 2, "PROBLEM"),
            CancellationToken.None);

        result.Outcome.Should().Be("created");

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == result.IncidentId);
        incident.DeviceId.Should().Be(device.Id);
    }

    [Fact]
    public async Task Problem_DuplicateOpen_ShouldBeIdempotent()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);
        var command = new IngestZabbixProblemCommand(
            "evt-400", "sw-zabbix", "192.0.2.71", "Ping down", 4, "PROBLEM");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.Outcome.Should().Be("created");
        second.Outcome.Should().Be("duplicate");
        second.IncidentId.Should().Be(first.IncidentId);
        (await context.Incidents.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Problem_SameTitleDifferentDevice_ShouldCreateBoth()
    {
        await using var context = await CreateContextAsync();
        await SeedDeviceAsync(context, ip: "192.0.2.71", name: "sw-zabbix");
        await SeedDeviceAsync(context, ip: "192.0.2.72", name: "sw-zabbix-2");
        var handler = Handler(context);

        var first = await handler.Handle(
            new IngestZabbixProblemCommand(
                null, "sw-zabbix", "192.0.2.71", "Ping down", 4, "PROBLEM"),
            CancellationToken.None);
        var second = await handler.Handle(
            new IngestZabbixProblemCommand(
                null, "sw-zabbix-2", "192.0.2.72", "Ping down", 4, "PROBLEM"),
            CancellationToken.None);

        first.Outcome.Should().Be("created");
        second.Outcome.Should().Be("created");
        second.IncidentId.Should().NotBe(first.IncidentId!.Value);
        (await context.Incidents.AsNoTracking().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Ok_ShouldResolveOpenIncidentWithSameKey()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);

        var created = await handler.Handle(
            new IngestZabbixProblemCommand(
                "evt-500", "sw-zabbix", "192.0.2.71", "Ping down", 4, "PROBLEM"),
            CancellationToken.None);
        var resolved = await handler.Handle(
            new IngestZabbixProblemCommand(
                "evt-500", "sw-zabbix", "192.0.2.71", "Ping down", 4, "OK"),
            CancellationToken.None);

        resolved.Outcome.Should().Be("resolved");
        resolved.IncidentId.Should().Be(created.IncidentId);

        var incident = await context.Incidents.AsNoTracking()
            .SingleAsync(row => row.Id == created.IncidentId);
        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Ok_WithoutOpenIncident_ShouldReturnNone()
    {
        await using var context = await CreateContextAsync();

        var result = await Handler(context).Handle(
            new IngestZabbixProblemCommand(
                "evt-600", "host-x", "203.0.113.9", "Janela de manutenção", 1, "OK"),
            CancellationToken.None);

        result.Outcome.Should().Be("none");
        result.IncidentId.Should().BeNull();
        (await context.Incidents.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Problem_AfterResolved_ShouldCreateNewIncident()
    {
        await using var context = await CreateContextAsync();
        var handler = Handler(context);
        var command = new IngestZabbixProblemCommand(
            null, "sw-zabbix", "192.0.2.71", "Ping down", 4, "PROBLEM");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(
            command with { Status = "OK" }, CancellationToken.None);
        var reopened = await handler.Handle(command, CancellationToken.None);

        reopened.Outcome.Should().Be("created");
        (await context.Incidents.AsNoTracking().CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(0, IncidentSeverity.Low)]
    [InlineData(1, IncidentSeverity.Low)]
    [InlineData(2, IncidentSeverity.Medium)]
    [InlineData(3, IncidentSeverity.High)]
    [InlineData(4, IncidentSeverity.Critical)]
    [InlineData(5, IncidentSeverity.Critical)]
    public async Task Severity_ZabbixLevels_ShouldMapToIncidentSeverity(
        int zabbixLevel, IncidentSeverity expected)
    {
        await using var context = await CreateContextAsync();

        await Handler(context).Handle(
            new IngestZabbixProblemCommand(
                null, null, null, $"Problema nível {zabbixLevel}", zabbixLevel, "PROBLEM"),
            CancellationToken.None);

        var incident = await context.Incidents.AsNoTracking().SingleAsync();
        incident.Severity.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task InvalidSeverity_ShouldThrowPtBr(int severity)
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(null, null, null, "Problema", severity, "PROBLEM"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage($"Severidade inválida: {severity}. Use 0 (Not classified) a 5 (Disaster).*");
    }

    [Fact]
    public async Task MissingSeverity_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(null, null, null, "Problema", null, "PROBLEM"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A severidade é obrigatória (0 a 5).*");
    }

    [Fact]
    public async Task EmptyName_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(null, null, null, "   ", 3, "PROBLEM"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome do problema é obrigatório.*");
    }

    [Fact]
    public async Task InvalidStatus_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new IngestZabbixProblemCommand(null, null, null, "Problema", 3, "DOWN"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Status inválido: 'DOWN'. Use 'PROBLEM' ou 'OK'.*");
    }
}
