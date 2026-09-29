using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
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
/// F7-T006: histórico ponta-a-ponta — create → assign → comentário
/// produzem entradas cronológicas com o ator no pipeline real.
/// </summary>
public sealed class TaskHistoryIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedUserAsync(
        AppDbContext dbContext, string email, string displayName)
    {
        var user = User.Create(email, displayName, UserRole.Admin);
        dbContext.DomainUsers.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task HistoryLifecycle_ShouldRecordChronologicalActions()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actorId = await SeedUserAsync(
            dbContext, "rita@exemplo.test", "Rita Operações");
        // Actor é o usuário logado (nome vem do join com DomainUsers).
        CurrentUser.UserId = actorId;
        CurrentUser.UserName = "rita@exemplo.test";

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Migrar VLAN 99", "Change"));

        await mediator.Send(new AssignTaskCommand(task.Id, actorId));
        await mediator.Send(
            new AddTaskCommentCommand(task.Id, "janela às 22h"));

        var entries = await mediator.Send(new GetTaskHistoryQuery(task.Id));

        entries.Select(entry => entry.Action)
            .Should().Equal(
                nameof(TaskHistoryAction.Created),
                nameof(TaskHistoryAction.Assigned),
                nameof(TaskHistoryAction.Commented));
        entries.Should().OnlyContain(entry => entry.ActorName == "Rita Operações");
        entries.ElementAt(1).Detail.Should().Be(actorId.ToString());
    }

    [Fact]
    public async Task GetHistory_WithoutTasksRead_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new GetTaskHistoryQuery(Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task GetHistory_UnknownTask_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetTaskHistoryQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}
