using FluentAssertions;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T002: GetDeviceResultsQuery — histórico de polls do device
/// (policy MonitoringRead, ordem mais-recente-primeiro, erros pt-BR).
/// </summary>
public sealed class GetDeviceResultsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GetDeviceResultsQueryTests()
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

    private static GetDeviceResultsQueryHandler Handler(AppDbContext context)
        => new(new DeviceRepository(context), new MonitoringResultRepository(context));

    private static async Task<Device> SeedDeviceAsync(AppDbContext context)
    {
        var location = Location.Create("Sala História");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, $"dev-{Guid.NewGuid():N}"[..12], DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task SeedResultsAsync(AppDbContext context, Device device, int count)
    {
        var start = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        var seeded = new List<(Guid Id, int Index)>();

        for (var i = 0; i < count; i++)
        {
            var row = MonitoringResult.Create(
                device.Id, CheckKind.Ping, succeeded: i % 2 == 0, durationMs: 5 + i);
            context.MonitoringResults.Add(row);
            seeded.Add((row.Id, i));
        }

        await context.SaveChangesAsync();

        // SaveChangesAsync sobrescreve CreateDateTime (auditoria) — o
        // histórico precisa de datas distintas, então ajusta via SQL.
        foreach (var (id, index) in seeded)
        {
            await context.Database.ExecuteSqlAsync(
                $"UPDATE monitoring_results SET \"CreateDateTime\" = {start.AddMinutes(index)} WHERE id = {id}");
        }
    }

    [Fact]
    public async Task Handle_ShouldReturnNewestFirstRespectingLimit()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedResultsAsync(context, device, count: 3);

        var result = await Handler(context).Handle(
            new GetDeviceResultsQuery(device.Id, Limit: 2), CancellationToken.None);

        result.DeviceId.Should().Be(device.Id);
        result.DeviceName.Should().Be(device.Name);
        result.Results.Should().HaveCount(2);
        // mais recente primeiro: minuto 2 e minuto 1
        result.Results[0].DurationMs.Should().Be(7);
        result.Results[1].DurationMs.Should().Be(6);
        result.Results.Select(r => r.Check).Should().OnlyContain(c => c == "Ping");
    }

    [Fact]
    public async Task Handle_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new GetDeviceResultsQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task Handle_InvalidLimit_ShouldThrowArgumentPtBrMessage(int limit)
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => Handler(context).Handle(
            new GetDeviceResultsQuery(device.Id, limit), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O limite deve estar entre 1 e 500.*");
    }
}
