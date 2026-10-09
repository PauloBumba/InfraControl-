using FluentAssertions;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Pdi;

/// <summary>
/// Fase 6 do PDI (docs/30 §15.3, §16, §23, ADR-036): avaliação final —
/// autoavaliação do colaborador (leva o plano a Em avaliação),
/// avaliação do gestor (exige a autoavaliação) e resultado final em 4
/// estados (§16) que conclui — ou replaneja — o plano; permissões e
/// comandos auditáveis ("PDI").
/// </summary>
public sealed class PdiEvaluationTests
{
    // ---- Autoavaliação (§15.3 — colaborador) ----------------------

    [Fact]
    public async Task SaveSelfAssessment_WhileInProgress_ShouldTransitionPlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var dto = await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(
                scene.Owner.UserId!.Value,
                "Desenvolvi delegação e comunicação clara com o time."),
            CancellationToken.None);

        dto.Side.Should().Be("Self");
        dto.Content.Should().Contain("delegação");

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.Status.Should().Be(PdiStatus.UnderEvaluation,
            "enviar a autoavaliação leva o plano a Em avaliação (§15.3)");

        var types = await context.PdiHistoryEntries
            .Select(entry => entry.Type)
            .ToListAsync();
        types.Should().Contain(PdiHistoryType.AssessmentCreated,
            "§17 — avaliação registrada no timeline");
        types.Should().Contain(PdiHistoryType.StatusChanged,
            "a transição automática entra no timeline");

        var changed = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.StatusChanged);
        changed.FromStatus.Should().Be(PdiStatus.InProgress);
        changed.ToStatus.Should().Be(PdiStatus.UnderEvaluation);
    }

    [Fact]
    public async Task SaveSelfAssessment_EmptyContent_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var act = () => Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "   "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*avaliação*");
        (await context.PdiAssessments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveSelfAssessment_Twice_ShouldUpdateSameRow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Primeira versão"),
            CancellationToken.None);
        await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Versão revisada"),
            CancellationToken.None);

        var assessment = await context.PdiAssessments.SingleAsync();
        assessment.Content.Should().Be("Versão revisada",
            "salvar de novo atualiza a mesma autoavaliação (upsert)");

        var created = await context.PdiHistoryEntries
            .CountAsync(entry => entry.Type == PdiHistoryType.AssessmentCreated);
        created.Should().Be(1, "só a criação gera evento — edição não repete");
    }

    [Fact]
    public async Task SaveSelfAssessment_WhenPlanFinished_ShouldConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        var actor = new PdiChangeActor(
            scene.Owner.UserId!.Value, "dono", "dono@exemplo.test");
        plan.Update(
            new PdiChanges(plan.Strategy, plan.TargetDate,
                PdiStatus.UnderEvaluation,
                plan.ManagerId, plan.PeriodStart, plan.PeriodEnd, Reason: null),
            actor);
        plan.Update(
            new PdiChanges(plan.Strategy, plan.TargetDate, PdiStatus.Completed,
                plan.ManagerId, plan.PeriodStart, plan.PeriodEnd, Reason: null),
            actor);
        await context.SaveChangesAsync();

        var act = () => Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Tarde demais"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- Avaliação do gestor (§15.3) -------------------------------

    [Fact]
    public async Task SaveManagerAssessment_WithoutSelfAssessment_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var act = () => Save(context, scene.Manager).Handle(
            new SavePdiAssessmentCommand(
                scene.Owner.UserId!.Value, "Avaliação do gestor sem base"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*autoavaliação*");
    }

    [Fact]
    public async Task SaveManagerAssessment_BeforePlanInEvaluation_ShouldConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        // Autoavaliação plantada direto no banco (sem o efeito colateral
        // de status) para isicionar só o guard de status do lado gestor.
        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        context.PdiAssessments.Add(PdiAssessment.Create(
            plan.Id, PdiAssessmentSide.Self, scene.Owner.UserId!.Value, "Base"));
        await context.SaveChangesAsync();

        var act = () => Save(context, scene.Manager).Handle(
            new SavePdiAssessmentCommand(
                scene.Owner.UserId!.Value, "Gestor adiantado"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*avaliação*");
    }

    [Fact]
    public async Task SaveManagerAssessment_AfterSelf_ShouldPersistForManager()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Auto"),
            CancellationToken.None);
        var dto = await Save(context, scene.Manager).Handle(
            new SavePdiAssessmentCommand(
                scene.Owner.UserId!.Value, "Ótima evolução no período"),
            CancellationToken.None);

        dto.Side.Should().Be("Manager");
        dto.AuthorId.Should().Be(scene.Manager.UserId!.Value);

        (await context.PdiAssessments.CountAsync()).Should().Be(2,
            "uma linha por lado (Self + Manager)");
    }

    [Fact]
    public async Task SaveAssessment_ByStranger_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var stranger = new FakeCurrentUser(Guid.NewGuid(), "Operator");

        var act = () => Save(context, stranger).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Intruso"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await context.PdiAssessments.CountAsync()).Should().Be(0);
    }

    // ---- Resultado final (§16) ------------------------------------

    [Fact]
    public async Task FinalizeResult_WithoutManagerAssessment_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Auto"),
            CancellationToken.None);

        var act = () => Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Atingido", Notes: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*gestor*");
    }

    [Fact]
    public async Task FinalizeResult_Atingido_ShouldCompletePlanAndRecordHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await SaveFullFlowAsync(context, scene);

        var dto = await Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Atingido",
                Notes: "Meta alcançada no período."),
            CancellationToken.None);

        dto.Outcome.Should().Be("Atingido");

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.Status.Should().Be(PdiStatus.Completed);

        var types = await context.PdiHistoryEntries
            .Select(entry => entry.Type)
            .ToListAsync();
        types.Should().Contain(PdiHistoryType.ResultFinalized);

        var finalized = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.ResultFinalized);
        finalized.Detail.Should().Contain("Atingido");
        finalized.ActorId.Should().Be(scene.Manager.UserId!.Value);
    }

    [Fact]
    public async Task FinalizeResult_ReplanjadoOutcome_ShouldReplanPlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await SaveFullFlowAsync(context, scene);

        await Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Replanejado",
                Notes: "Ciclo reaberto com novas metas."),
            CancellationToken.None);

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.Status.Should().Be(PdiStatus.Replanned,
            "resultado 'Replanejado' devolve o plano ao ciclo (§16 + §3)");
    }

    [Fact]
    public async Task FinalizeResult_Twice_ShouldConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await SaveFullFlowAsync(context, scene);

        await Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Atingido", Notes: null),
            CancellationToken.None);

        var act = () => Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "NaoAtingido", Notes: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resultado*");
        (await context.PdiResults.CountAsync()).Should().Be(1,
            "resultado final é único por plano");
    }

    [Fact]
    public async Task FinalizeResult_InvalidOutcome_ShouldFail400()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await SaveFullFlowAsync(context, scene);

        var act = () => Finalize(context, scene.Manager).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Whatever", Notes: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*inválido*");
    }

    [Fact]
    public async Task FinalizeResult_ByStranger_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await SaveFullFlowAsync(context, scene);
        var stranger = new FakeCurrentUser(Guid.NewGuid(), "Operator");

        var act = () => Finalize(context, stranger).Handle(
            new FinalizePdiResultCommand(
                scene.Owner.UserId!.Value, "Atingido", Notes: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---- Auditoria (Fase 5 → continuada) --------------------------

    [Fact]
    public void EvaluationCommands_ShouldBeAuditableAsPdi()
    {
        ((IAuditableRequest)new SavePdiAssessmentCommand(
            Guid.NewGuid(), "conteúdo")).AuditResource.Should().Be("PDI");
        ((IAuditableRequest)new FinalizePdiResultCommand(
            Guid.NewGuid(), "Atingido", null)).AuditResource.Should().Be("PDI");
    }

    // ---- Helpers --------------------------------------------------

    private static async Task SaveFullFlowAsync(
        AppDbContext context, SceneActors scene)
    {
        await Save(context, scene.Owner).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Auto"),
            CancellationToken.None);
        await Save(context, scene.Manager).Handle(
            new SavePdiAssessmentCommand(scene.Owner.UserId!.Value, "Gestor"),
            CancellationToken.None);
    }

    private static async Task<SceneActors> SeedSceneAsync(AppDbContext context)
    {
        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");

        // Plano direto em Em acompanhamento — a Fase 6 entra na execução.
        var plan = IndividualDevelopmentPlan.Create(
            owner.Id, "Plano do ciclo", new DateOnly(2027, 6, 30),
            PdiStatus.InProgress, manager.Id, null, null,
            new PdiChangeActor(owner.Id, "Ana Dono", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        context.PdiHistoryEntries.AddRange(plan.DrainPendingHistory());
        await context.SaveChangesAsync();

        return new SceneActors(
            new FakeCurrentUser(owner.Id, "Operator"),
            new FakeCurrentUser(manager.Id, "Operator"));
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static SavePdiAssessmentCommandHandler Save(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<SavePdiAssessmentCommandHandler>.Instance);

    private static FinalizePdiResultCommandHandler Finalize(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<FinalizePdiResultCommandHandler>.Instance);

    private sealed record SceneActors(FakeCurrentUser Owner, FakeCurrentUser Manager);

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public Guid Id => UserId!.Value;
        public string? UserName => $"ator-{UserId}@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
