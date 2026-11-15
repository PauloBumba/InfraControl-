using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Commands;
using InfraControl.Application.Alerts.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// Atribuição/transferência do responsável pelo alerta
/// (`AssignAlertCommand`/`UnassignAlertCommand`): Operate, 404 pt-BR para
/// alerta/usuário desconhecido, reatribuir = transferência.
/// </summary>
public sealed class AlertAssignCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertAssignCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private static async Task<(AppDbContext Context, Alert Alert, User User)> SeedAsync(
        DbContextOptions<AppDbContext> options)
    {
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var location = Location.Create("Sala Assign");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var device = Device.Create(
            location.Id, "sw-assign", DeviceType.Switch, ip: "192.0.2.90");
        context.Devices.Add(device);

        var user = User.Create(Guid.NewGuid(), "op@example.com", "Operador Um", UserRole.Operator);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Warning, "latência alta");
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();
        return (context, alert, user);
    }

    [Fact]
    public void Assign_ShouldRequireOperate()
        => new AssignAlertCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public void Unassign_ShouldRequireOperate()
        => new UnassignAlertCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task Assign_ShouldSetAssigneeAndReturnName()
    {
        var (context, alert, user) = await SeedAsync(_options);
        await using var _ = context;

        var dto = await new AssignAlertCommandHandler(new AlertRepository(context))
            .Handle(new AssignAlertCommand(alert.Id, user.Id), CancellationToken.None);

        dto.AssignedToId.Should().Be(user.Id);
        dto.AssignedToName.Should().Be("Operador Um");
    }

    [Fact]
    public async Task Assign_Twice_ShouldTransfer()
    {
        var (context, alert, user) = await SeedAsync(_options);
        await using var _ = context;

        var other = User.Create(Guid.NewGuid(), "op2@example.com", "Operador Dois", UserRole.Operator);
        context.DomainUsers.Add(other);
        await context.SaveChangesAsync();

        var handler = new AssignAlertCommandHandler(new AlertRepository(context));
        await handler.Handle(new AssignAlertCommand(alert.Id, user.Id), CancellationToken.None);
        var dto = await handler.Handle(new AssignAlertCommand(alert.Id, other.Id), CancellationToken.None);

        dto.AssignedToId.Should().Be(other.Id);
        dto.AssignedToName.Should().Be("Operador Dois");
    }

    [Fact]
    public async Task Assign_UnknownUser_ShouldThrowNotFound()
    {
        var (context, alert, _) = await SeedAsync(_options);
        await using var _ = context;

        var act = () => new AssignAlertCommandHandler(new AlertRepository(context))
            .Handle(new AssignAlertCommand(alert.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task Unassign_ShouldClearAssignee()
    {
        var (context, alert, user) = await SeedAsync(_options);
        await using var _ = context;

        var handler = new UnassignAlertCommandHandler(new AlertRepository(context));
        await new AssignAlertCommandHandler(new AlertRepository(context))
            .Handle(new AssignAlertCommand(alert.Id, user.Id), CancellationToken.None);
        var dto = await handler.Handle(new UnassignAlertCommand(alert.Id), CancellationToken.None);

        dto.AssignedToId.Should().BeNull();
    }

    [Fact]
    public async Task Search_ShouldPreferAlertAssigneeOverIncident()
    {
        var (context, alert, user) = await SeedAsync(_options);
        await using var _ = context;

        var incidentUser = User.Create(Guid.NewGuid(), "inc@example.com", "Resp Incidente", UserRole.Operator);
        context.DomainUsers.Add(incidentUser);
        var incident = Incident.Create("Incidente teste", IncidentSeverity.High, alert.DeviceId);
        incident.Assign(incidentUser.Id);
        context.Incidents.Add(incident);
        context.IncidentAlerts.Add(IncidentAlert.Create(incident.Id, alert.Id));
        await context.SaveChangesAsync();

        // Sem responsável próprio → cai para o do incidente.
        var before = await new SearchAlertsQueryHandler(new AlertRepository(context))
            .Handle(new SearchAlertsQuery(), CancellationToken.None);
        before.Items.Single().AssignedToName.Should().Be("Resp Incidente");

        // Com responsável próprio → o do alerta vence.
        alert.Assign(user.Id);
        await context.SaveChangesAsync();

        var after = await new SearchAlertsQueryHandler(new AlertRepository(context))
            .Handle(new SearchAlertsQuery(), CancellationToken.None);
        var item = after.Items.Single();
        item.AssignedToId.Should().Be(user.Id);
        item.AssignedToName.Should().Be("Operador Um");
    }
}
