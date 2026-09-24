using FluentAssertions;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T001: GetDeviceChecksQuery — lista os MonitoringCheck de um device
/// (policy MonitoringRead, erros em pt-BR).
/// </summary>
public sealed class GetDeviceChecksQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GetDeviceChecksQueryTests()
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

    private static GetDeviceChecksQueryHandler Handler(AppDbContext context)
        => new(new DeviceRepository(context), new MonitoringCheckRepository(context));

    private static async Task<Device> SeedPrinterWithChecksAsync(AppDbContext context)
    {
        var location = Location.Create("Ala Impressoras");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, $"prn-{Guid.NewGuid():N}"[..12], DeviceType.Printer);
        context.Devices.Add(device);
        foreach (var kind in device.MonitoringChecks)
            context.MonitoringChecks.Add(MonitoringCheck.Create(device.Id, kind));
        await context.SaveChangesAsync();
        return device;
    }

    [Fact]
    public async Task Handle_Printer_ShouldReturnChecksOrderedByKind()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedPrinterWithChecksAsync(context);

        var result = await Handler(context).Handle(
            new GetDeviceChecksQuery(device.Id), CancellationToken.None);

        result.DeviceId.Should().Be(device.Id);
        result.DeviceName.Should().Be(device.Name);
        result.Checks.Select(check => check.Check)
            .Should().Equal("Ping", "SnmpToner", "SnmpDrum");
        result.Checks.Should().OnlyContain(check => check.Enabled);
        result.Checks.Should().OnlyContain(check => check.Id != Guid.Empty);
    }

    [Fact]
    public async Task Handle_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new GetDeviceChecksQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }
}
