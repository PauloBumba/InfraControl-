using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T006/K6: geração de rascunho (spec docs/32 §2–§4, ADR-035) —
/// montagem DETERMINÍSTICA dos registros existentes (nunca inventa:
/// seção sem registro é omitida), sempre nasce Draft → revisão, com
/// versão v1.0 (K3) e origem Task/Pdi (K4).
/// </summary>
public sealed class KnowledgeGenerationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeGenerationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        if (!await context.DomainUsers.AnyAsync(user => user.Id == CurrentUserId))
        {
            context.DomainUsers.Add(User.Create(
                CurrentUserId, "autor@exemplo.test", "Autor do Teste",
                UserRole.Operator));
            await context.SaveChangesAsync();
        }
        return context;
    }

    private sealed class FakeCurrentUser(string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = CurrentUserId;
        public string? UserName => "autor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static GenerateTaskKnowledgeCommandHandler TaskGenerateHandler(
        AppDbContext context, string[]? roles = null)
        => new(
            new TaskItemRepository(context),
            new TaskHistoryRepository(context),
            new TaskCriterionRepository(context),
            new TaskPhaseRepository(context),
            new TaskEvidenceRepository(context),
            new TaskCommentRepository(context),
            new TaskImpedimentRepository(context),
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(roles ?? ["Operator"]),
            NullLogger<GenerateTaskKnowledgeCommandHandler>.Instance);

    private static GeneratePdiKnowledgeCommandHandler PdiGenerateHandler(
        AppDbContext context, string[]? roles = null)
        => new(
            new PdiRepository(context),
            new PdiStructureRepository(context),
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(roles ?? ["Operator"]),
            NullLogger<GeneratePdiKnowledgeCommandHandler>.Instance);

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static async Task<TaskItem> SeedConcludedTaskAsync(
        AppDbContext context, string title, string? description)
    {
        var task = TaskItem.Create(title, TaskKind.Manual, description);
        // Workflow F7-T007: Open → InProgress → Resolved.
        task.ChangeState(TaskState.InProgress);
        task.ChangeState(TaskState.Resolved);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private static async Task<Guid> SeedPlanAsync(
        AppDbContext context, PdiStatus status, Guid? ownerId = null)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId ?? CurrentUserId,
            "Estratégia de desenvolvimento profissional.",
            new DateOnly(2027, 6, 30), status,
            null, null, null,
            new PdiChangeActor(
                ownerId ?? CurrentUserId,
                "gestor@empresa.test", "dono@empresa.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan.Id;
    }

    [Fact]
    public async Task GenerateTaskKnowledge_ConcludedTask_ShouldCreateDraftWithRecords()
    {
        await using var context = await CreateContextAsync();
        var task = await SeedConcludedTaskAsync(
            context, "Troca do switch do CTI",
            "Substituir o switch de acesso do CTI.");
        context.TaskCriteria.Add(
            TaskCriterion.Create(task.Id, "Ping contínuo por 24h"));
        context.TaskImpediments.Add(
            TaskImpediment.Create(task.Id, "Janela de manutenção restrita"));
        context.TaskComments.Add(
            TaskComment.Create(task.Id, CurrentUserId, "Peça chegou."));
        context.TaskPhases.Add(
            TaskPhase.Create(task.Id, "Execução", 1, isCurrent: true));
        context.TaskEvidences.Add(TaskEvidence.Create(
            task.Id, "ping.txt", "text/plain", 128, "Resultado do ping."));
        context.TaskHistories.Add(TaskHistory.Create(
            task.Id, TaskHistoryAction.Created, CurrentUserId, "task criada"));
        await context.SaveChangesAsync();

        var dto = await TaskGenerateHandler(context).Handle(
            new GenerateTaskKnowledgeCommand(task.Id), CancellationToken.None);

        // Nasce Draft → revisão (§2; ADR-034) e usa o título da task.
        dto.Status.Should().Be(KnowledgeArticleStatus.Draft);
        dto.Title.Should().Be("Troca do switch do CTI");

        // Só registros existentes (§3 — nunca inventar).
        dto.Content.Should().Contain("Rascunho gerado automaticamente");
        dto.Content.Should().Contain("## Objetivo")
            .And.Contain("Substituir o switch de acesso do CTI.");
        dto.Content.Should().Contain("## Critérios de aceite")
            .And.Contain("Ping contínuo por 24h");
        dto.Content.Should().Contain("## Dificuldades e impedimentos")
            .And.Contain("Janela de manutenção restrita");
        dto.Content.Should().Contain("## Fases")
            .And.Contain("Execução");
        dto.Content.Should().Contain("## Evidências")
            .And.Contain("ping.txt");
        dto.Content.Should().Contain("## Comentários")
            .And.Contain("Peça chegou.");
        dto.Content.Should().Contain("## Linha do tempo")
            .And.Contain("task criada");

        // K3: v1.0 com motivo de proveniência.
        var version = await context.KnowledgeArticleVersions.SingleAsync(
            row => row.ArticleId == dto.Id);
        version.Version.Should().Be("1.0");
        version.Reason.Should().StartWith("Geração automática");

        // K4: "criada a partir de" — origem Task.
        var detail = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(dto.Id), CancellationToken.None);
        detail.Origins.Should().ContainSingle(origin =>
            origin.Type == "Task" && origin.Id == task.Id);
    }

    [Fact]
    public async Task GenerateTaskKnowledge_OpenTask_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var task = TaskItem.Create("Em aberto", TaskKind.Manual, null);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var action = () => TaskGenerateHandler(context).Handle(
            new GenerateTaskKnowledgeCommand(task.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("A task precisa estar concluída para gerar Knowledge.");
    }

    [Fact]
    public async Task GenerateTaskKnowledge_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => TaskGenerateHandler(context).Handle(
            new GenerateTaskKnowledgeCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    /// <summary>§3: seção sem registro correspondente não é escrita.</summary>
    [Fact]
    public async Task GenerateTaskKnowledge_WithoutRecords_ShouldOmitEmptySections()
    {
        await using var context = await CreateContextAsync();
        var task = await SeedConcludedTaskAsync(
            context, "Tarefa limpa", "Descrição registrada.");

        var dto = await TaskGenerateHandler(context).Handle(
            new GenerateTaskKnowledgeCommand(task.Id), CancellationToken.None);

        dto.Content.Should().Contain("Rascunho gerado automaticamente");
        dto.Content.Should().Contain("## Objetivo");
        dto.Content.Should().NotContain("## Linha do tempo");
        dto.Content.Should().NotContain("## Critérios de aceite");
        dto.Content.Should().NotContain("## Comentários");
        dto.Content.Should().NotContain("## Evidências");
    }

    [Fact]
    public async Task GeneratePdiKnowledge_CompletedPdi_ShouldCreateDraftFromObjective()
    {
        await using var context = await CreateContextAsync();
        var planId = await SeedPlanAsync(context, PdiStatus.Completed);
        context.PdiObjectives.Add(PdiObjective.Create(
            CurrentUserId,
            "Conhecer funcionamento do sistema de Tomografia",
            "Foco na operação diária.",
            null, null, null, null, null, null, order: 1));
        await context.SaveChangesAsync();

        var dto = await PdiGenerateHandler(context).Handle(
            new GeneratePdiKnowledgeCommand(CurrentUserId),
            CancellationToken.None);

        // Título vem do primeiro objetivo (§4) — sem reescrita mágica.
        dto.Status.Should().Be(KnowledgeArticleStatus.Draft);
        dto.Title.Should().Be("Conhecer funcionamento do sistema de Tomografia");
        dto.Content.Should().Contain("Rascunho gerado automaticamente");
        dto.Content.Should().Contain("## Plano");
        dto.Content.Should().Contain("## Objetivos");
        dto.Content.Should().Contain(
            "### 1. Conhecer funcionamento do sistema de Tomografia");
        dto.Content.Should().Contain("Foco na operação diária.");

        // K4: origem Pdi apontando para o plano.
        var detail = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(dto.Id), CancellationToken.None);
        detail.Origins.Should().ContainSingle(origin =>
            origin.Type == "Pdi" && origin.Id == planId);
    }

    [Fact]
    public async Task GeneratePdiKnowledge_DraftPdi_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        await SeedPlanAsync(context, PdiStatus.Draft);

        var action = () => PdiGenerateHandler(context).Handle(
            new GeneratePdiKnowledgeCommand(CurrentUserId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O PDI precisa estar concluído para gerar Knowledge.");
    }

    [Fact]
    public async Task GeneratePdiKnowledge_UnknownUser_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        // Admin pula a checagem de dono (mesma regra dos endpoints de PDI).
        var action = () => PdiGenerateHandler(context, ["Admin"]).Handle(
            new GeneratePdiKnowledgeCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"PDI do usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task GeneratePdiKnowledge_OtherUsersPdi_ShouldThrowUnauthorized()
    {
        await using var context = await CreateContextAsync();
        var otherId = Guid.NewGuid();
        await SeedPlanAsync(context, PdiStatus.Completed, otherId);

        var action = () => PdiGenerateHandler(context).Handle(
            new GeneratePdiKnowledgeCommand(otherId), CancellationToken.None);

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Você só pode gerar Knowledge do seu próprio PDI.");
    }
}
