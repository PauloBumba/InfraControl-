using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// ADR-030 §1 — CRUD completo de MonitoringCheck: create (guard de device
/// 404 + duplicidade por índice (DeviceId, Kind) → 409 + CheckKind inválido
/// → 400), update (toggle Enabled por id), delete e get by id (404 pt-BR).
/// </summary>
public sealed class MonitoringCheckCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MonitoringCheckCommandTests()
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

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, $"sw-{Guid.NewGuid():N}"[..8], DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static CreateMonitoringCheckCommandHandler CreateHandler(AppDbContext context)
        => new(
            new MonitoringCheckRepository(context),
            new DeviceRepository(context),
            NullLogger<CreateMonitoringCheckCommandHandler>.Instance);

    private static UpdateMonitoringCheckCommandHandler UpdateHandler(AppDbContext context)
        => new(new MonitoringCheckRepository(context));

    private static DeleteMonitoringCheckCommandHandler DeleteHandler(AppDbContext context)
        => new(new MonitoringCheckRepository(context));

    private static GetMonitoringCheckQueryHandler GetHandler(AppDbContext context)
        => new(new MonitoringCheckRepository(context));

    [Fact]
    public async Task Create_Valid_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var dto = await CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "Ping"), CancellationToken.None);

        dto.Id.Should().NotBe(Guid.Empty);
        dto.DeviceId.Should().Be(deviceId);
        dto.Check.Should().Be("Ping");
        dto.Enabled.Should().BeTrue();
        context.MonitoringChecks.Should().ContainSingle(c => c.Id == dto.Id);
    }

    [Fact]
    public async Task Create_UnknownDevice_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(unknownId, "Ping"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateKind_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        await CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "Ping"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "Ping"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Device já possui a check 'Ping'.");
    }

    [Fact]
    public async Task Create_InvalidCheck_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "FancyCheck"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Check inválido: 'FancyCheck'.*");
    }

    [Fact]
    public async Task Update_Existing_ShouldToggleEnabled()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "Ping"), CancellationToken.None);

        var dto = await UpdateHandler(context).Handle(
            new UpdateMonitoringCheckCommand(created.Id, Enabled: false),
            CancellationToken.None);

        dto.Enabled.Should().BeFalse();
        dto.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateMonitoringCheckCommand(unknownId, Enabled: false), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Check '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task Delete_Existing_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "Ping"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteMonitoringCheckCommand(created.Id), CancellationToken.None);

        context.MonitoringChecks.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteMonitoringCheckCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Check '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task GetById_Existing_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMonitoringCheckCommand(deviceId, "SnmpSystem"), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetMonitoringCheckQuery(created.Id), CancellationToken.None);

        dto.Id.Should().Be(created.Id);
        dto.DeviceId.Should().Be(deviceId);
        dto.Check.Should().Be("SnmpSystem");
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => GetHandler(context).Handle(
            new GetMonitoringCheckQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Check '{unknownId}' não encontrada.");
    }
}
