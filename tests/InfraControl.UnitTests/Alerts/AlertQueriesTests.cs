using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Alerts.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// F6-T004: filtro de status da busca de alertas ganha "suppressed"
/// (estados da UI: open|suppressed|resolved); entrada inválida → 400 pt-BR.
/// </summary>
public sealed class AlertQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertQueriesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, SearchAlertsQueryHandler Handler)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return (context, new SearchAlertsQueryHandler(new AlertRepository(context)));
    }

    [Fact]
    public async Task Search_WithStatusSuppressed_ShouldAcceptAndReturnMatching()
    {
        var (context, handler) = await CreateAsync();
        var location = Location.Create("Sala Queries");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-q", DeviceType.Switch, ip: "192.0.2.70");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var open = Alert.Create(device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        var suppressed = Alert.Create(
            device.Id, CheckKind.SnmpToner, AlertSeverity.Warning, "toner 18%");
        suppressed.Suppress();
        context.Alerts.AddRange(open, suppressed);
        await context.SaveChangesAsync();

        var page = await handler.Handle(
            new SearchAlertsQuery(Status: "suppressed"), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(nameof(AlertStatus.Suppressed));
        item.Id.Should().Be(suppressed.Id);
    }

    [Fact]
    public async Task Search_WithInvalidStatus_ShouldThrowArgumentErrorPtBr()
    {
        var (context, handler) = await CreateAsync();

        var action = () => handler.Handle(
            new SearchAlertsQuery(Status: "bugged"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Status inválido*suppressed*");
    }
}
