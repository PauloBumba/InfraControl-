using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F7-T001: CRUD da Task — guards pt-BR (400/404), filtros de busca
/// (termo/tipo/estado) e permissões TasksRead/TasksManage.
/// </summary>
public sealed class TaskCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskCommandTests()
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

    private static TaskItemRepository Repo(AppDbContext context) => new(context);
    private static TaskAssigneeRepository AssigneeRepo(AppDbContext context) => new(context);
    private static TaskDeviceRepository DeviceRepo(AppDbContext context) => new(context);
    private static TaskIncidentRepository IncidentRepo(AppDbContext context) => new(context);

    private static RecordingTaskHistoryWriter History() => new();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "criador@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Operator"];
    }

    private static CreateTaskCommandHandler CreateHandler(
        AppDbContext context, Guid? creatorId = null)
        => new(Repo(context), AssigneeRepo(context), History(),
            new FakeCurrentUser(creatorId ?? Guid.NewGuid()),
            NullLogger<CreateTaskCommandHandler>.Instance);

    // ── Create ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldRequireOperatorOrAdmin()
        => new CreateTaskCommand("t", "manual").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task Create_ShouldOpenTaskAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var creatorId = Guid.NewGuid();

        var dto = await CreateHandler(context, creatorId).Handle(
            new CreateTaskCommand(
                "Trocar filtro do rack B",
                "Maintenance",
                "agendada com o datacenter"),
            CancellationToken.None);

        dto.Title.Should().Be("Trocar filtro do rack B");
        dto.Kind.Should().Be(nameof(TaskKind.Maintenance));
        dto.State.Should().Be(nameof(TaskState.Open));
        dto.Description.Should().Be("agendada com o datacenter");

        // F32-T001 — o criador vem do usuário autenticado.
        dto.CreatedById.Should().Be(creatorId);

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.State.Should().Be(TaskState.Open);
        saved.CreatedById.Should().Be(creatorId);
    }

    [Fact]
    public async Task Create_WithoutTitle_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateTaskCommand("  ", "manual"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título da task é obrigatório.*");
    }

    [Fact]
    public async Task Create_UnknownKind_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateTaskCommand("t", "besouro"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'besouro'.*");
    }

    // ── Update / Delete ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldChangeTitleAndDescription()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateTaskCommand("antes", "manual"), CancellationToken.None);

        await new UpdateTaskCommandHandler(Repo(context), History(), NullLogger<UpdateTaskCommandHandler>.Instance)
            .Handle(new UpdateTaskCommand(created.Id, "depois", "nova desc"),
                CancellationToken.None);

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.Title.Should().Be("depois");
        saved.Description.Should().Be("nova desc");
    }

    [Fact]
    public async Task Update_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => new UpdateTaskCommandHandler(
                Repo(context), History(), NullLogger<UpdateTaskCommandHandler>.Instance)
            .Handle(new UpdateTaskCommand(unknown, "t"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Delete_ShouldRemoveTask()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateTaskCommand("descartar", "manual"), CancellationToken.None);

        await new DeleteTaskCommandHandler(Repo(context), NullLogger<DeleteTaskCommandHandler>.Instance)
            .Handle(new DeleteTaskCommand(created.Id), CancellationToken.None);

        context.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => new DeleteTaskCommandHandler(
                Repo(context), NullLogger<DeleteTaskCommandHandler>.Instance)
            .Handle(new DeleteTaskCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    // ── Search / Get ─────────────────────────────────────────────────────

    [Fact]
    public void Queries_ShouldRequireAnyRole()
    {
        new SearchTasksQuery().RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
        new GetTaskQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    [Fact]
    public async Task Search_ShouldFilterByTermKindAndState()
    {
        await using var context = await CreateContextAsync();
        var create = CreateHandler(context);
        var created = await create.Handle(
            new CreateTaskCommand("Trocar UPS da sala fria", "Preventive"),
            CancellationToken.None);
        await create.Handle(
            new CreateTaskCommand("Inventário de portas", "Discovery"),
            CancellationToken.None);
        await create.Handle(
            new CreateTaskCommand("Post-mortem do outage", "Incident"),
            CancellationToken.None);
        await new UpdateTaskCommandHandler(Repo(context), History(), NullLogger<UpdateTaskCommandHandler>.Instance)
            .Handle(new UpdateTaskCommand(created.Id, "Trocar UPS da sala fria",
                "troca preventiva agendada"), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(Repo(context), AssigneeRepo(context), DeviceRepo(context), IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));

        var byTerm = await handler.Handle(
            new SearchTasksQuery(Term: "UPS"), CancellationToken.None);
        byTerm.Items.Should().ContainSingle();

        var byKind = await handler.Handle(
            new SearchTasksQuery(Kind: "discovery"), CancellationToken.None);
        byKind.Items.Should().ContainSingle()
            .Which.Kind.Should().Be(nameof(TaskKind.Discovery));

        var byState = await handler.Handle(
            new SearchTasksQuery(State: "open"), CancellationToken.None);
        byState.TotalCount.Should().Be(3);

        var open = await handler.Handle(
            new SearchTasksQuery(Kind: "preventive", State: "open"),
            CancellationToken.None);
        open.Items.Should().ContainSingle().Which.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Search_InvalidKindOrState_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var handler = new SearchTasksQueryHandler(Repo(context), AssigneeRepo(context), DeviceRepo(context), IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));

        var kindAction = () => handler.Handle(
            new SearchTasksQuery(Kind: "besouro"), CancellationToken.None);
        (await kindAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'besouro'.*");

        var stateAction = () => handler.Handle(
            new SearchTasksQuery(State: "voando"), CancellationToken.None);
        (await stateAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Estado inválido: 'voando'.*");
    }

    [Fact]
    public async Task Get_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => new GetTaskQueryHandler(Repo(context), AssigneeRepo(context), DeviceRepo(context), IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context)).Handle(
            new GetTaskQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}
