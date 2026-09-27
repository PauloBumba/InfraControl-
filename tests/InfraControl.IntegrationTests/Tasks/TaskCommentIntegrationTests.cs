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
/// F7-T005: comentários ponta-a-ponta — autor = usuário logado, listagem
/// cronológica com DisplayName no pipeline real.
/// </summary>
public sealed class TaskCommentIntegrationTests : BaseIntegrationTest
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
    public async Task CommentLifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authorId = await SeedUserAsync(
            dbContext, "lia@exemplo.test", "Lia Suporte");
        // Autor é o usuário logado (FK author_id Restrict exige user real).
        CurrentUser.UserId = authorId;
        CurrentUser.UserName = "lia@exemplo.test";

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Revisar patch do core", "Change"));

        var created = await mediator.Send(
            new AddTaskCommentCommand(task.Id, "patch aplicado às 14h"));
        created.AuthorId.Should().Be(authorId);

        var comments = await mediator.Send(
            new GetTaskCommentsQuery(task.Id));
        var comment = comments.Should().ContainSingle().Subject;
        comment.Content.Should().Be("patch aplicado às 14h");
        comment.AuthorName.Should().Be("Lia Suporte");

        var second = await mediator.Send(
            new AddTaskCommentCommand(task.Id, "validação em produção"));
        second.AuthorId.Should().Be(authorId);

        var both = await mediator.Send(new GetTaskCommentsQuery(task.Id));
        both.Select(item => item.Content)
            .Should().Equal("patch aplicado às 14h", "validação em produção");
    }

    [Fact]
    public async Task GetComments_WithoutTasksRead_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(new GetTaskCommentsQuery(Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: TasksRead");
    }

    [Fact]
    public async Task AddComment_WithoutTasksManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(
            new AddTaskCommentCommand(Guid.NewGuid(), "comentário"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: TasksManage");
    }

    [Fact]
    public async Task AddComment_UnknownTask_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new AddTaskCommentCommand(unknown, "x"));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}
