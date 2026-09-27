using FluentAssertions;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

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
        UseNoPermissions();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new SearchIncidentsQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*IncidentsRead*");
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
            .WithMessage("*IncidentsManage*");
    }
}
