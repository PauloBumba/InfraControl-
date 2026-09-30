using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace InfraControl.IntegrationTests.Incidents;

/// <summary>
/// F6-T003: fluxo ponta-a-ponta dos incidentes — criação manual, vínculo
/// N:N com alert, acknowledge e troca de status no pipeline real
/// (AuthorizationBehavior + PostgreSQL do Testcontainers).
/// </summary>
public sealed class IncidentIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ManualLifecycle_CreateAcknowledgeResolve_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.60");
        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        dbContext.Alerts.Add(alert);
        await dbContext.SaveChangesAsync();
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var created = await mediator.Send(new CreateIncidentCommand(
            "Switch principal fora do ar", IncidentSeverity.Critical,
            DeviceId: device.Id));
        created.Status.Should().Be(nameof(IncidentStatus.Open));

        var acknowledged = await mediator.Send(
            new AcknowledgeIncidentCommand(created.Id));
        acknowledged.Status.Should().Be(nameof(IncidentStatus.Investigating));

        var resolved = await mediator.Send(
            new UpdateIncidentStatusCommand(created.Id, "resolved"));
        resolved.Status.Should().Be(nameof(IncidentStatus.Resolved));
        resolved.ResolvedAt.Should().NotBeNull();

        var page = await mediator.Send(new SearchIncidentsQuery());
        var item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(created.Id);
        item.AlertCount.Should().Be(0);

        var fetched = await mediator.Send(new GetIncidentQuery(created.Id));
        fetched.Title.Should().Be("Switch principal fora do ar");
    }

    [Fact]
    public async Task SearchIncidents_WithoutIncidentsRead_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseNoRoles();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new SearchIncidentsQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task CreateIncident_WithoutIncidentsManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(
            new CreateIncidentCommand("x", IncidentSeverity.Low));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task AutoOpenedIncident_ShouldExposeRootCauseViaQuery()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(context, ip: "192.0.2.61");
        for (var i = 2; i >= 1; i--)
        {
            var row = MonitoringResult.Create(
                device.Id, CheckKind.Ping, succeeded: false, 5, "erro");
            context.MonitoringResults.Add(row);
            await context.SaveChangesAsync();
            var at = DateTime.UtcNow.AddHours(-i);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE monitoring_results SET \"CreateDateTime\" = {0} WHERE id = {1}",
                at, row.Id);
        }

        var evaluator = scope.ServiceProvider.GetRequiredService<IAlertEvaluator>();
        await evaluator.EvaluateAsync(
            device, CheckKind.Ping,
            MonitoringResult.Create(
                device.Id, CheckKind.Ping, succeeded: false, 5, "erro"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        UseRole(UserRole.Admin);
        var page = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new SearchIncidentsQuery());

        var item = page.Items.Should().ContainSingle().Subject;
        item.DeviceId.Should().Be(device.Id);
        item.RootCause.Should().Be(
            "Falha local no device (energia, processo ou enlace direto).",
            "F6-T005: causa provável derivada exposta na leitura");
    }

    [Fact]
    public async Task RunCheckPingUp_ShouldResolveLinkedAlertAndCloseIncident()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(context, ip: "192.0.2.62");

        var alert = Alert.Create(
            device.Id, CheckKind.Ping, AlertSeverity.Critical, "down");
        context.Alerts.Add(alert);
        var incident = Incident.Create(
            "fora do ar", IncidentSeverity.Critical, deviceId: device.Id);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        context.IncidentAlerts.Add(IncidentAlert.Create(incident.Id, alert.Id));
        await context.SaveChangesAsync();

        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        // ICMP falso sempre sobe → resolve o alerta e dispara o recovery
        var run = await mediator.Send(new RunCheckCommand(device.Id, "Ping"));
        run.Succeeded.Should().BeTrue();

        var incidents = await mediator.Send(new SearchIncidentsQuery());
        var item = incidents.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(nameof(IncidentStatus.Resolved));
        item.ResolvedAt.Should().NotBeNull(
            "F6-T006: todos os alerts vinculados resolved → fecha");
    }

    /// <summary>
    /// F16-T002: criação manual levanta IncidentCreatedDomainEvent na mesma
    /// transação (ADR-005 — AppDbContext despacha → OutboxMessage).
    /// </summary>
    [Fact]
    public async Task ManualCreate_ShouldWriteIncidentCreatedEventToOutbox()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var created = await mediator.Send(new CreateIncidentCommand(
            "Servidor de backup fora do ar", IncidentSeverity.High));

        created.Status.Should().Be(nameof(IncidentStatus.Open));
        var outboxEvents = await context.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == nameof(IncidentCreatedDomainEvent))
            .ToListAsync();
        // Payload é JSON com escape Unicode (JavaScriptEncoder default):
        // compara o Title parseado, não o texto cru.
        outboxEvents.Should().ContainSingle();
        var payloadTitle = JsonDocument
            .Parse(outboxEvents[0].Payload).RootElement.GetProperty("Title").GetString();
        payloadTitle.Should().Be("Servidor de backup fora do ar");
    }
}
