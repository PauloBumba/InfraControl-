using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T008: UpdateDeviceProfileCommand (override/reset) com repositório real.
/// </summary>
public sealed class DeviceProfileCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DeviceProfileCommandTests()
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

    private static async Task<Device> SeedDeviceAsync(AppDbContext context, DeviceType type = DeviceType.Printer)
    {
        var location = Location.Create("Sala A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, $"dev-{Guid.NewGuid():N}"[..12], type);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static UpdateDeviceProfileCommandHandler Handler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new MonitoringCheckRepository(context),
            NullLogger<UpdateDeviceProfileCommandHandler>.Instance);

    [Fact]
    public async Task Update_WithChecks_ShouldApplyCustomProfile()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, DeviceType.Switch);

        await Handler(context).Handle(
            new UpdateDeviceProfileCommand(device.Id, [CheckKind.Ping]),
            CancellationToken.None);

        var stored = context.Devices.Single(d => d.Id == device.Id);
        stored.MonitoringChecks.Should().Equal(CheckKind.Ping);
        stored.ProfileIsCustom.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithUseDefault_ShouldResetToTypeDefaults()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, DeviceType.Printer);
        device.ApplyCustomProfile([CheckKind.Ping]);
        await context.SaveChangesAsync();

        await Handler(context).Handle(
            new UpdateDeviceProfileCommand(device.Id, Checks: null, UseDefault: true),
            CancellationToken.None);

        var stored = context.Devices.Single(d => d.Id == device.Id);
        stored.MonitoringChecks.Should().Equal(
            CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum);
        stored.ProfileIsCustom.Should().BeFalse();
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new UpdateDeviceProfileCommand(unknownId, [CheckKind.Ping]),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_NeitherChecksNorUseDefault_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => Handler(context).Handle(
            new UpdateDeviceProfileCommand(device.Id, Checks: null, UseDefault: false),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Informe os checks ou useDefault.*");
    }
}
