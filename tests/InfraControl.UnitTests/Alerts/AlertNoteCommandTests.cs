using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// F50-T003 — anotação do operador no alerta (`SetAlertOperatorNoteCommand`):
/// PUT de substituição (texto grava, null/branco limpa), guarda de 500 e
/// 404 pt-BR para alerta desconhecido.
/// </summary>
public sealed class AlertNoteCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertNoteCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private static async Task<(AppDbContext Context, Alert Alert)> SeedAsync(
        DbContextOptions<AppDbContext> options)
    {
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var location = Location.Create("Sala Notas");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-note", DeviceType.Switch, ip: "192.0.2.80");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();
        return (context, alert);
    }

    private static SetAlertOperatorNoteCommandHandler Handler(AppDbContext context)
        => new(new AlertRepository(context));

    [Fact]
    public void SetNote_ShouldRequireOperate()
        => new SetAlertOperatorNoteCommand(Guid.NewGuid(), "nota").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task SetNote_WithText_ShouldPersistOnAlert()
    {
        var (context, alert) = await SeedAsync(_options);

        var dto = await Handler(context).Handle(
            new SetAlertOperatorNoteCommand(alert.Id, "  troca do switch em andamento  "),
            CancellationToken.None);

        dto.OperatorNote.Should().Be("troca do switch em andamento");
        var saved = await context.Alerts.AsNoTracking().SingleAsync();
        saved.OperatorNote.Should().Be("troca do switch em andamento");
    }

    [Fact]
    public async Task SetNote_WithNullOrBlank_ShouldClearExistingNote()
    {
        var (context, alert) = await SeedAsync(_options);
        await Handler(context).Handle(
            new SetAlertOperatorNoteCommand(alert.Id, "primeira nota"),
            CancellationToken.None);

        await Handler(context).Handle(
            new SetAlertOperatorNoteCommand(alert.Id, "   "),
            CancellationToken.None);

        var saved = await context.Alerts.AsNoTracking().SingleAsync();
        saved.OperatorNote.Should().BeNull("PUT substitui — branco limpa");
    }

    [Fact]
    public async Task SetNote_UnknownAlert_ShouldThrowNotFoundPtBr()
    {
        var (context, _) = await SeedAsync(_options);
        var unknown = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new SetAlertOperatorNoteCommand(unknown, "nota"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Alerta '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task SetNote_AboveMax_ShouldThrowArgumentPtBr()
    {
        var (context, alert) = await SeedAsync(_options);

        var action = () => Handler(context).Handle(
            new SetAlertOperatorNoteCommand(alert.Id, new string('x', 501)),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage($"*{Alert.OperatorNoteMaxLength}*");
    }
}
