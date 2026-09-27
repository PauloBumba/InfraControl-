using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using InfraControl.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Tasks;

/// <summary>
/// F7-T002: atribuição ponta-a-ponta — assign → get/search com ids →
/// unassign no pipeline real (AuthorizationBehavior + PostgreSQL).
/// </summary>
public sealed class TaskAssigneeIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedUserAsync(
        AppDbContext dbContext, string email, string displayName)
    {
        var user = User.Create(email, displayName, UserRole.User);
        dbContext.DomainUsers.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task AssignLifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync(
            dbContext, "carlos@exemplo.test", "Carlos Redes");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Subir ACL no firewall", "Change"));

        await mediator.Send(new AssignTaskCommand(task.Id, userId));

        var fetched = await mediator.Send(new GetTaskQuery(task.Id));
        fetched.AssigneeCount.Should().Be(1);
        fetched.AssigneeIds.Should().Equal(userId);

        var page = await mediator.Send(
            new SearchTasksQuery(Term: "firewall"));
        page.Items.Should().ContainSingle()
            .Which.AssigneeIds.Should().Equal(userId);

        await mediator.Send(new UnassignTaskCommand(task.Id, userId));

        var after = await mediator.Send(new GetTaskQuery(task.Id));
        after.AssigneeCount.Should().Be(0);
        after.AssigneeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Assign_Duplicate_ShouldConflict409PtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync(
            dbContext, "dina@exemplo.test", "Dina Campo");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(new CreateTaskCommand("Rotear VLAN", "Change"));
        await mediator.Send(new AssignTaskCommand(task.Id, userId));

        var action = () => mediator.Send(new AssignTaskCommand(task.Id, userId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O usuário já está atribuído à task.");
    }

    [Fact]
    public async Task Assign_WithoutTasksManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(
            new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: TasksManage");
    }
}
