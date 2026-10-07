using FluentAssertions;
using InfraControl.Application.Tasks;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4d — timeline unificada tipada (§8): composição em leitura
/// de histórico + comentários, 13 tipos, ação `Commented` excluída
/// (comentário vem da fonte rica), filtro `types` (desconhecido → 400
/// pt-BR), `limit` com clamp e ordenação por data desc.
/// </summary>
public sealed class TaskTimelineQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskTimelineQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static GetTaskTimelineQueryHandler Handler(AppDbContext context) =>
        new(
            new TaskItemRepository(context),
            new TaskHistoryRepository(context),
            new TaskCommentRepository(context));

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task da timeline", TaskKind.Incident);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    [Fact]
    public void Query_ShouldRequireReadRoles()
    {
        new GetTaskTimelineQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    [Fact]
    public async Task Query_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new GetTaskTimelineQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Query_ShouldMergeHistoryAndCommentsNewestFirst()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var actor = User.Create(
            "ator@exemplo.test", "Ana Ator", UserRole.Admin);
        context.DomainUsers.Add(actor);
        context.TaskHistories.Add(TaskHistory.Create(
            taskId, TaskHistoryAction.StateChanged, actor.Id, "aberta → em andamento"));
        await context.SaveChangesAsync();

        await Task.Delay(15); // ordenação por data exige carimbos distintos
        context.TaskComments.Add(TaskComment.Create(
            taskId, actor.Id, "Comentário na timeline"));
        await context.SaveChangesAsync();

        var entries = await Handler(context).Handle(
            new GetTaskTimelineQuery(taskId), CancellationToken.None);

        entries.Should().HaveCount(2);
        var newest = entries.ToArray();
        newest[0].Type.Should().Be(TaskTimelineTypes.Comment);
        newest[0].Comment.Should().Be("Comentário na timeline");
        newest[0].ActorName.Should().Be("Ana Ator");
        newest[1].Type.Should().Be(TaskTimelineTypes.State);
        newest[1].Action.Should().Be(nameof(TaskHistoryAction.StateChanged));
        newest[1].Detail.Should().Be("aberta → em andamento");
        newest[1].ActorName.Should().Be("Ana Ator");
        newest[1].Id.Should().StartWith("h:");
    }

    [Fact]
    public async Task Query_ShouldExcludeCommentedActionAndUseCommentSource()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        // Ação `Commented` do histórico + a linha rica do comentário:
        // só a fonte rica pode aparecer (sem duplicata). author_id é FK.
        var author = User.Create(
            "autor@exemplo.test", "Autor Comentário", UserRole.Operator);
        context.DomainUsers.Add(author);
        context.TaskHistories.Add(TaskHistory.Create(
            taskId, TaskHistoryAction.Commented, detail: "duplicata"));
        context.TaskComments.Add(TaskComment.Create(
            taskId, author.Id, "Texto real do comentário"));
        await context.SaveChangesAsync();

        var entries = await Handler(context).Handle(
            new GetTaskTimelineQuery(taskId), CancellationToken.None);

        entries.Should().ContainSingle();
        entries.Single().Type.Should().Be(TaskTimelineTypes.Comment);
        entries.Single().Id.Should().StartWith("c:");
        entries.Single().Comment.Should().Be("Texto real do comentário");
    }

    [Fact]
    public async Task Query_TypeFilter_ShouldReturnOnlyRequestedType()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var author = User.Create(
            "filtro@exemplo.test", "Autor Filtro", UserRole.Operator);
        context.DomainUsers.Add(author);
        context.TaskHistories.Add(TaskHistory.Create(
            taskId, TaskHistoryAction.StateChanged, detail: "x → y"));
        context.TaskHistories.Add(TaskHistory.Create(
            taskId, TaskHistoryAction.Assigned));
        context.TaskComments.Add(TaskComment.Create(
            taskId, author.Id, "comentário"));
        await context.SaveChangesAsync();

        var entries = await Handler(context).Handle(
            new GetTaskTimelineQuery(taskId, Types: "estado"),
            CancellationToken.None);

        entries.Should().ContainSingle();
        entries.Single().Type.Should().Be(TaskTimelineTypes.State);
    }

    [Fact]
    public async Task Query_UnknownType_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => Handler(context).Handle(
            new GetTaskTimelineQuery(taskId, Types: "qualquer"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Tipo de timeline inválido: qualquer*");
    }

    [Fact]
    public async Task Query_ShouldMapActionsToTimelineTypes()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        TaskHistoryAction[] actions =
        [
            TaskHistoryAction.Created,
            TaskHistoryAction.Assigned,
            TaskHistoryAction.PriorityChanged,
            TaskHistoryAction.DeviceLinked,
            TaskHistoryAction.SlaPaused,
            TaskHistoryAction.PhaseCompleted,
            TaskHistoryAction.ImpedimentResolved,
            TaskHistoryAction.EvidenceAdded,
            TaskHistoryAction.CriterionAdded,
            TaskHistoryAction.SubtaskAdded,
            TaskHistoryAction.Updated,
        ];
        foreach (var action in actions)
            context.TaskHistories.Add(TaskHistory.Create(taskId, action));
        await context.SaveChangesAsync();

        var entries = await Handler(context).Handle(
            new GetTaskTimelineQuery(taskId), CancellationToken.None);

        var byType = entries.ToDictionary(
            entry => entry.Action!, entry => entry.Type);
        byType.Should().HaveCount(actions.Length);
        byType[nameof(TaskHistoryAction.Created)]
            .Should().Be(TaskTimelineTypes.Creation);
        byType[nameof(TaskHistoryAction.Assigned)]
            .Should().Be(TaskTimelineTypes.Assignment);
        byType[nameof(TaskHistoryAction.PriorityChanged)]
            .Should().Be(TaskTimelineTypes.Planning);
        byType[nameof(TaskHistoryAction.DeviceLinked)]
            .Should().Be(TaskTimelineTypes.Link);
        byType[nameof(TaskHistoryAction.SlaPaused)]
            .Should().Be(TaskTimelineTypes.Sla);
        byType[nameof(TaskHistoryAction.PhaseCompleted)]
            .Should().Be(TaskTimelineTypes.Phase);
        byType[nameof(TaskHistoryAction.ImpedimentResolved)]
            .Should().Be(TaskTimelineTypes.Blocker);
        byType[nameof(TaskHistoryAction.EvidenceAdded)]
            .Should().Be(TaskTimelineTypes.Attachment);
        byType[nameof(TaskHistoryAction.CriterionAdded)]
            .Should().Be(TaskTimelineTypes.Criterion);
        byType[nameof(TaskHistoryAction.SubtaskAdded)]
            .Should().Be(TaskTimelineTypes.Subtask);
        byType[nameof(TaskHistoryAction.Updated)]
            .Should().Be(TaskTimelineTypes.Edit);
    }

    [Fact]
    public async Task Query_Limit_ShouldTakeNewestEntries()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        for (var i = 0; i < 3; i++)
        {
            context.TaskHistories.Add(TaskHistory.Create(
                taskId, TaskHistoryAction.Updated, detail: $"edicao {i}"));
            await context.SaveChangesAsync();
            await Task.Delay(15);
        }

        var entries = await Handler(context).Handle(
            new GetTaskTimelineQuery(taskId, Limit: 2), CancellationToken.None);

        entries.Should().HaveCount(2);
        entries.Select(entry => entry.Detail)
            .Should().ContainInOrder("edicao 2", "edicao 1");
    }
}
