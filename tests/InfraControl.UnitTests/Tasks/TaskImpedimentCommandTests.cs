using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4b — impedimentos estruturados (§10): CRUD no molde dos
/// critérios — 404 pt-BR, 400 do domínio, um histórico por aspecto
/// alterado (ImpedimentAdded/Updated/Resolved/Reopened/Removed) com
/// truncagem ≤200, no-op sem registro e query em ordem de criação.
/// </summary>
public sealed class TaskImpedimentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskImpedimentCommandTests()
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
    private static TaskImpedimentRepository ImpedimentRepo(AppDbContext context) => new(context);

    private static AddTaskImpedimentCommandHandler AddHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            Repo(context),
            ImpedimentRepo(context),
            history,
            NullLogger<AddTaskImpedimentCommandHandler>.Instance);

    private static UpdateTaskImpedimentCommandHandler UpdateHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            ImpedimentRepo(context),
            history,
            NullLogger<UpdateTaskImpedimentCommandHandler>.Instance);

    private static DeleteTaskImpedimentCommandHandler DeleteHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            ImpedimentRepo(context),
            history,
            NullLogger<DeleteTaskImpedimentCommandHandler>.Instance);

    private static GetTaskImpedimentsQueryHandler QueryHandler(AppDbContext context)
        => new(Repo(context), ImpedimentRepo(context));

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task para impedimentos", TaskKind.Incident);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<Guid> SeedImpedimentAsync(
        AppDbContext context, Guid taskId, string description = "Impedimento seed")
    {
        var impediment = TaskImpediment.Create(taskId, description);
        context.TaskImpediments.Add(impediment);
        await context.SaveChangesAsync();
        return impediment.Id;
    }

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueryRead()
    {
        new AddTaskImpedimentCommand(Guid.NewGuid(), "x").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateTaskImpedimentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteTaskImpedimentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetTaskImpedimentsQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Add ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskImpedimentCommand(unknown, "Energia no rack"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Add_ShouldPersistAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await AddHandler(context, history).Handle(
            new AddTaskImpedimentCommand(taskId, "Aguardando aprovação do orçamento"),
            CancellationToken.None);

        dto.TaskId.Should().Be(taskId);
        dto.Description.Should().Be("Aguardando aprovação do orçamento");
        dto.ResolvedAt.Should().BeNull();

        var saved = await context.TaskImpediments.AsNoTracking()
            .SingleAsync(impediment => impediment.Id == dto.Id);
        saved.TaskId.Should().Be(taskId);

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentAdded);
        history.Records[0].Detail.Should().Be("Aguardando aprovação do orçamento");
    }

    [Fact]
    public async Task Add_InvalidDescription_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var emptyAction = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskImpedimentCommand(taskId, "   "),
                CancellationToken.None);
        (await emptyAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A descrição do impedimento é obrigatória.*");

        var longAction = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskImpedimentCommand(taskId, new string('x', 301)),
                CancellationToken.None);
        (await longAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*no máximo 300 caracteres.*");
    }

    [Fact]
    public async Task Add_LongDescription_ShouldTruncateHistoryDetail()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        await AddHandler(context, history).Handle(
            new AddTaskImpedimentCommand(taskId, new string('d', 300)),
            CancellationToken.None);

        var detail = history.Records.Single().Detail;
        detail!.Length.Should().BeLessThanOrEqualTo(TaskHistory.DetailMaxLength);
        detail.Should().EndWith("…");
    }

    // ── Update ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldEditDescriptionAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(
            context, taskId, "Descrição original");
        var history = new RecordingTaskHistoryWriter();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(
                impedimentId, Description: "Descrição revisada"),
            CancellationToken.None);

        dto.Description.Should().Be("Descrição revisada");

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentUpdated);
        history.Records[0].Detail.Should()
            .Be("descrição: Descrição original → Descrição revisada");
    }

    [Fact]
    public async Task Update_Resolve_ShouldStampAndRecordResolved()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(context, taskId, "Bloqueio de rede");
        var history = new RecordingTaskHistoryWriter();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(impedimentId, Resolved: true),
            CancellationToken.None);

        dto.ResolvedAt.Should().NotBeNull();

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentResolved);
        history.Records[0].Detail.Should().Be("resolvido: Bloqueio de rede");
    }

    [Fact]
    public async Task Update_Reopen_ShouldClearAndRecordReopened()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(context, taskId, "Fornecedor atrasou");
        var history = new RecordingTaskHistoryWriter();
        await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(impedimentId, Resolved: true),
            CancellationToken.None);
        history.Records.Clear();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(impedimentId, Resolved: false),
            CancellationToken.None);

        dto.ResolvedAt.Should().BeNull();

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentReopened);
        history.Records[0].Detail.Should().Be("reaberto: Fornecedor atrasou");
    }

    [Fact]
    public async Task Update_DescriptionAndResolved_ShouldRecordTwoEntries()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(context, taskId, "Antes");
        var history = new RecordingTaskHistoryWriter();

        await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(
                impedimentId, Description: "Depois", Resolved: true),
            CancellationToken.None);

        history.Records.Should().HaveCount(2);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentUpdated);
        history.Records[1].Action.Should().Be(TaskHistoryAction.ImpedimentResolved);
        history.Records.Should().OnlyContain(record => record.TaskId == taskId);
    }

    [Fact]
    public async Task Update_NoOp_ShouldNotRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(context, taskId, "Estável");
        var history = new RecordingTaskHistoryWriter();

        // Mesma descrição e resolução já em aberto (null) = no-op.
        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskImpedimentCommand(impedimentId, Description: "Estável"),
            CancellationToken.None);

        dto.Description.Should().Be("Estável");
        history.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_UnknownImpediment_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new UpdateTaskImpedimentCommand(unknown, Resolved: true),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Impedimento '{unknown}' não encontrado.");
    }

    // ── Delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ShouldRemoveAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var impedimentId = await SeedImpedimentAsync(context, taskId, "Para excluir");
        var history = new RecordingTaskHistoryWriter();

        await DeleteHandler(context, history).Handle(
            new DeleteTaskImpedimentCommand(impedimentId),
            CancellationToken.None);

        (await context.TaskImpediments.AsNoTracking()
            .CountAsync(impediment => impediment.Id == impedimentId))
            .Should().Be(0);

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ImpedimentRemoved);
        history.Records[0].Detail.Should().Be("Para excluir");
    }

    [Fact]
    public async Task Delete_UnknownImpediment_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context, new RecordingTaskHistoryWriter())
            .Handle(new DeleteTaskImpedimentCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Impedimento '{unknown}' não encontrado.");
    }

    // ── Query ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_ShouldListByCreationOrder()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var first = await SeedImpedimentAsync(context, taskId, "Primeiro");
        // CreateDateTime pode cair no mesmo tick — espera entre seeds
        // para a ordenação por criação ser determinística.
        await Task.Delay(20);
        var second = await SeedImpedimentAsync(context, taskId, "Segundo");

        var rows = await QueryHandler(context).Handle(
            new GetTaskImpedimentsQuery(taskId), CancellationToken.None);

        rows.Select(impediment => impediment.Id)
            .Should().ContainInOrder(first, second);
        rows.Should().OnlyContain(
            impediment => impediment.TaskId == taskId && impediment.ResolvedAt == null);
    }

    [Fact]
    public async Task Query_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetTaskImpedimentsQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}
