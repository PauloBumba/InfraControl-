using FluentAssertions;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Tasks;

/// <summary>
/// F7-T004: vínculo Task ↔ Incident ponta-a-ponta — link → get/search
/// com ids → unlink no pipeline real.
/// </summary>
public sealed class TaskIncidentIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task LinkLifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var incident = await mediator.Send(
            new CreateIncidentCommand("Bundle LACP em queda", IncidentSeverity.High));
        var task = await mediator.Send(
            new CreateTaskCommand("Investigar bundle", "Incident"));

        await mediator.Send(new LinkTaskIncidentCommand(task.Id, incident.Id));

        var fetched = await mediator.Send(new GetTaskQuery(task.Id));
        fetched.IncidentCount.Should().Be(1);
        fetched.IncidentIds.Should().Equal(incident.Id);

        var page = await mediator.Send(new SearchTasksQuery(Term: "bundle"));
        page.Items.Should().ContainSingle()
            .Which.IncidentIds.Should().Equal(incident.Id);

        await mediator.Send(new UnlinkTaskIncidentCommand(task.Id, incident.Id));

        var after = await mediator.Send(new GetTaskQuery(task.Id));
        after.IncidentCount.Should().Be(0);
        after.IncidentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Link_Duplicate_ShouldConflict409PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var incident = await mediator.Send(
            new CreateIncidentCommand("Power supply zerado", IncidentSeverity.Critical));
        var task = await mediator.Send(new CreateTaskCommand("Trocar PSU", "Corrective"));
        await mediator.Send(new LinkTaskIncidentCommand(task.Id, incident.Id));

        var action = () => mediator.Send(
            new LinkTaskIncidentCommand(task.Id, incident.Id));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O incidente já está vinculado à task.");
    }

    [Fact]
    public async Task LinkIncident_WithoutTasksManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new LinkTaskIncidentCommand(Guid.NewGuid(), Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }
}
