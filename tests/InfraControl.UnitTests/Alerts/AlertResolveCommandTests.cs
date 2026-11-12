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
/// F52-T003 — baixa justificada do alerta (`ResolveAlertCommand`):
/// código + justificativa obrigatórios, resolução automática intocada
/// (code/note nulos), guarda de 500 e 404 pt-BR.
/// </summary>
public sealed class AlertResolveCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertResolveCommandTests()
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

        var location = Location.Create("Sala Resolve");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-resolve", DeviceType.Switch, ip: "192.0.2.81");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Warning, "latência alta");
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();
        return (context, alert);
    }

    private static ResolveAlertCommandHandler Handler(AppDbContext context)
        => new(new AlertRepository(context));

    [Fact]
    public void ResolveAlert_ShouldRequireOperate()
        => new ResolveAlertCommand(Guid.NewGuid(), "Fixed", "nota").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task Resolve_WithCodeAndNote_ShouldPersistResolution()
    {
        var (context, alert) = await SeedAsync(_options);

        var dto = await Handler(context).Handle(
            new ResolveAlertCommand(alert.Id, "Workaround", "  monitoramento já cobre  "),
            CancellationToken.None);

        dto.Status.Should().Be(nameof(AlertStatus.Resolved));
        dto.ResolutionCode.Should().Be("Workaround");
        dto.ResolutionNote.Should().Be("monitoramento já cobre");
        dto.ResolvedAt.Should().NotBeNull();

        var saved = await context.Alerts.AsNoTracking().SingleAsync();
        saved.ResolutionCode.Should().Be(ResolutionCode.Workaround);
        saved.ResolutionNote.Should().Be("monitoramento já cobre");
    }

    [Fact]
    public async Task AutoResolve_ShouldLeaveCodeAndNoteNull()
    {
        var (context, alert) = await SeedAsync(_options);

        alert.Resolve(); // resolução automática pós-check
        await context.SaveChangesAsync();

        var saved = await context.Alerts.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(AlertStatus.Resolved);
        saved.ResolutionCode.Should().BeNull("auto-resolve não tem baixa manual");
        saved.ResolutionNote.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_WithoutNote_ShouldThrowArgumentPtBr()
    {
        var (context, alert) = await SeedAsync(_options);

        var action = () => Handler(context).Handle(
            new ResolveAlertCommand(alert.Id, "Fixed", "   "),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*justificativa da baixa é obrigatória*");
    }

    [Fact]
    public async Task Resolve_WithInvalidCode_ShouldEchoItPtBr()
    {
        var (context, alert) = await SeedAsync(_options);

        var action = () => Handler(context).Handle(
            new ResolveAlertCommand(alert.Id, "banana", "nota"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*banana*");
    }

    [Fact]
    public async Task Resolve_AboveMaxNote_ShouldThrowArgumentPtBr()
    {
        var (context, alert) = await SeedAsync(_options);

        var action = () => Handler(context).Handle(
            new ResolveAlertCommand(alert.Id, "Fixed", new string('x', 501)),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage($"*{Alert.ResolutionNoteMaxLength}*");
    }

    [Fact]
    public async Task Resolve_UnknownAlert_ShouldThrowNotFoundPtBr()
    {
        var (context, _) = await SeedAsync(_options);
        var unknown = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new ResolveAlertCommand(unknown, "Fixed", "nota"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Alerta '{unknown}' não encontrado.");
    }
}
