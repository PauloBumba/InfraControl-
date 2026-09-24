using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T001: materialização das linhas MonitoringCheck a partir do
/// DeviceProfile — create do device, override e reset do perfil, cascade no
/// delete. Handlers reais com Sqlite in-memory.
/// </summary>
public sealed class MonitoringCheckMaterializationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MonitoringCheckMaterializationTests()
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

    private static async Task<Guid> SeedLocationAsync(AppDbContext context)
    {
        var location = Location.Create("Sala Monitoramento");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    private static CreateDeviceCommandHandler CreateHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new LocationRepository(context),
            new RackRepository(context),
            new MonitoringCheckRepository(context),
            NullLogger<CreateDeviceCommandHandler>.Instance);

    private static UpdateDeviceProfileCommandHandler ProfileHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new MonitoringCheckRepository(context),
            NullLogger<UpdateDeviceProfileCommandHandler>.Instance);

    private static DeleteDeviceCommandHandler DeleteHandler(AppDbContext context)
        => new(new DeviceRepository(context), NullLogger<DeleteDeviceCommandHandler>.Instance);

    private static async Task<List<MonitoringCheck>> RowsAsync(AppDbContext context, Guid deviceId)
    {
        var rows = await context.MonitoringChecks
            .AsNoTracking()
            .Where(check => check.DeviceId == deviceId)
            .ToListAsync();
        return rows.OrderBy(check => check.Kind).ToList();
    }

    private static async Task<Guid> CreateDeviceAsync(
        AppDbContext context, DeviceType type, string name)
    {
        var locationId = await SeedLocationAsync(context);
        var dto = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, name, type.ToString()),
            CancellationToken.None);
        return dto.Id;
    }

    [Fact]
    public async Task Create_Printer_ShouldMaterializeTypeDefaultChecks()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await CreateDeviceAsync(context, DeviceType.Printer, "printer-01");

        var rows = await RowsAsync(context, deviceId);

        rows.Should().HaveCount(3);
        rows.Select(row => row.Kind).Should()
            .Equal(CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum);
        rows.Should().OnlyContain(row => row.Enabled);
    }

    [Fact]
    public async Task Create_Workstation_ShouldMaterializePingOnly()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await CreateDeviceAsync(context, DeviceType.Workstation, "pc-01");

        var rows = await RowsAsync(context, deviceId);

        rows.Select(row => row.Kind).Should().Equal(CheckKind.Ping);
    }

    [Fact]
    public async Task UpdateProfile_Apply_ShouldAddRemoveAndPreserveExistingRows()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await CreateDeviceAsync(context, DeviceType.Switch, "switch-01");
        var pingRowId = (await RowsAsync(context, deviceId))
            .Single(row => row.Kind == CheckKind.Ping).Id;

        await ProfileHandler(context).Handle(
            new UpdateDeviceProfileCommand(deviceId, [CheckKind.Ping, CheckKind.SnmpToner]),
            CancellationToken.None);

        var rows = await RowsAsync(context, deviceId);
        rows.Select(row => row.Kind).Should().Equal(CheckKind.Ping, CheckKind.SnmpToner);
        rows.Single(row => row.Kind == CheckKind.Ping).Id.Should().Be(pingRowId);
        rows.Single(row => row.Kind == CheckKind.Ping).Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfile_Reset_ShouldRestoreTypeDefaultRows()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await CreateDeviceAsync(context, DeviceType.Printer, "printer-02");
        var pingRowId = (await RowsAsync(context, deviceId))
            .Single(row => row.Kind == CheckKind.Ping).Id;

        await ProfileHandler(context).Handle(
            new UpdateDeviceProfileCommand(deviceId, [CheckKind.Ping]),
            CancellationToken.None);
        await ProfileHandler(context).Handle(
            new UpdateDeviceProfileCommand(deviceId, Checks: null, UseDefault: true),
            CancellationToken.None);

        var rows = await RowsAsync(context, deviceId);
        rows.Select(row => row.Kind).Should()
            .Equal(CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum);
        rows.Single(row => row.Kind == CheckKind.Ping).Id.Should().Be(pingRowId);
    }

    [Fact]
    public async Task DeleteDevice_ShouldRemoveCheckRowsViaCascade()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await CreateDeviceAsync(context, DeviceType.Printer, "printer-03");
        (await RowsAsync(context, deviceId)).Should().HaveCount(3);

        await DeleteHandler(context).Handle(
            new DeleteDeviceCommand(deviceId), CancellationToken.None);

        (await RowsAsync(context, deviceId)).Should().BeEmpty();
    }
}
