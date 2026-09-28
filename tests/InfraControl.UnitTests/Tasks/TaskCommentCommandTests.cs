using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F7-T005: comentário da Task — autor = usuário logado (401 pt-BR),
/// 404 pt-BR (task), 400 pt-BR (conteúdo) e listagem com autor.
/// </summary>
public sealed class TaskCommentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskCommentCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static TaskItemRepository TaskRepo(AppDbContext context) => new(context);
    private static TaskCommentRepository CommentRepo(AppDbContext context) => new(context);

    private static async Task<(Guid TaskId, Guid AuthorId)> SeedTaskAndAuthorAsync(
        AppDbContext context)
    {
        var task = TaskItem.Create("Task com comentários", TaskKind.Manual);
        context.Tasks.Add(task);

        var author = User.Create(
            "bia@exemplo.test", "Bia Infra", UserRole.User);
        context.DomainUsers.Add(author);
        await context.SaveChangesAsync();
        return (task.Id, author.Id);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "bia@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["User"];
        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class AnonymousCurrentUser : ICurrentUserContext
    {
        public Guid? UserId => null;
        public string? UserName => null;
        public bool IsAuthenticated => false;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static AddTaskCommentCommandHandler AddHandler(
        AppDbContext context, ICurrentUserContext user)
        => new(TaskRepo(context), CommentRepo(context), History(), user,
            NullLogger<AddTaskCommentCommandHandler>.Instance);

    private static GetTaskCommentsQueryHandler ListHandler(AppDbContext context)
        => new(TaskRepo(context), CommentRepo(context));

    // ── Add ──────────────────────────────────────────────────────────────

    [Fact]
    public void Command_ShouldRequireTasksManage()
        => new AddTaskCommentCommand(Guid.NewGuid(), "c").RequiredPermissions
            .Should().Equal(Permission.TasksManage.ToString());

    [Fact]
    public async Task Add_ShouldUseLoggedUserAsAuthor()
    {
        await using var context = await CreateContextAsync();
        var (taskId, authorId) = await SeedTaskAndAuthorAsync(context);

        var dto = await AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(taskId, "comentei aqui"),
            CancellationToken.None);

        dto.TaskId.Should().Be(taskId);
        dto.AuthorId.Should().Be(authorId);
        dto.Content.Should().Be("comentei aqui");

        var saved = await context.TaskComments.AsNoTracking().SingleAsync();
        saved.AuthorId.Should().Be(authorId);
    }

    [Fact]
    public async Task Add_WithoutAuthenticatedUser_ShouldThrowUnauthorizedPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndAuthorAsync(context);

        var action = () => AddHandler(context, new AnonymousCurrentUser()).Handle(
            new AddTaskCommentCommand(taskId, "x"), CancellationToken.None);

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Autenticação é necessária para esta requisição.");
    }

    [Fact]
    public async Task Add_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, authorId) = await SeedTaskAndAuthorAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(unknown, "x"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Add_BlankContent_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, authorId) = await SeedTaskAndAuthorAsync(context);

        var action = () => AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(taskId, "  "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O conteúdo do comentário é obrigatório.*");
    }

    // ── List ─────────────────────────────────────────────────────────────

    [Fact]
    public void Query_ShouldRequireTasksRead()
        => new GetTaskCommentsQuery(Guid.NewGuid()).RequiredPermissions
            .Should().Equal(Permission.TasksRead.ToString());

    [Fact]
    public async Task List_ShouldReturnChronologicalCommentsWithAuthorName()
    {
        await using var context = await CreateContextAsync();
        var (taskId, authorId) = await SeedTaskAndAuthorAsync(context);
        var author = await context.DomainUsers.AsNoTracking()
            .SingleAsync(user => user.Id == authorId);
        await AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(taskId, "primeiro"), CancellationToken.None);
        await AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(taskId, "segundo"), CancellationToken.None);

        var comments = await ListHandler(context).Handle(
            new GetTaskCommentsQuery(taskId), CancellationToken.None);

        comments.Select(comment => comment.Content)
            .Should().Equal("primeiro", "segundo");
        comments.Should().OnlyContain(comment => comment.AuthorName == author.DisplayName);
    }

    [Fact]
    public async Task List_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ListHandler(context).Handle(
            new GetTaskCommentsQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task List_WithoutOtherTasks_ShouldReturnOnlyTaskComments()
    {
        await using var context = await CreateContextAsync();
        var (taskId, authorId) = await SeedTaskAndAuthorAsync(context);
        var other = TaskItem.Create("outra task", TaskKind.Manual);
        context.Tasks.Add(other);
        await context.SaveChangesAsync();
        await AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(taskId, "único"), CancellationToken.None);
        await AddHandler(context, new FakeCurrentUser(authorId)).Handle(
            new AddTaskCommentCommand(other.Id, "de outra"), CancellationToken.None);

        var comments = await ListHandler(context).Handle(
            new GetTaskCommentsQuery(taskId), CancellationToken.None);

        comments.Should().ContainSingle().Which.Content.Should().Be("único");
    }
}
