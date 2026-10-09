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
/// Fase 5 do PDI (docs/30 §14, §15, §17, ADR-035): governança —
/// alterações de campos exigem motivo e registram valor anterior →
/// novo no timeline; aprovação de fase (concluir → validar/rejeitar →
/// próxima fase) sem bypass pelo PUT genérico; eventos de check-in e
/// impedimento na timeline; comandos PDI auditáveis ("PDI").
/// </summary>
public sealed class PdiGovernanceTests
{
    // ---- §14 Replanejamento (motivo + valores no histórico) --------

    [Fact]
    public async Task Update_WhenFieldsChangeWithoutReason_ShouldFailAndKeepState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var handler = Upsert(context, scene.Owner);

        var noReason = () => handler.Handle(
            new UpsertPdiCommand(
                scene.Owner.UserId!.Value, "Plano do ciclo",
                new DateOnly(2027, 12, 31), "Active"),
            CancellationToken.None);
        await noReason.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*motivo*");

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.TargetDate.Should().Be(new DateOnly(2027, 6, 30),
            "estado intacto após a recusa");
        (await context.PdiHistoryEntries.CountAsync()).Should().Be(1,
            "só a entrada Created — nada foi alterado");
    }

    [Fact]
    public async Task Update_WithReason_ShouldRecordOldAndNewValuesInHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var handler = Upsert(context, scene.Owner);

        await handler.Handle(
            new UpsertPdiCommand(
                scene.Owner.UserId!.Value, "Plano revisado",
                new DateOnly(2027, 12, 31), "Active",
                Reason: "Replanejamento aprovado em reunião"),
            CancellationToken.None);

        var updated = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.Updated);
        updated.Reason.Should().Be("Replanejamento aprovado em reunião");
        updated.ActorId.Should().Be(scene.Owner.UserId!.Value);
        updated.Detail.Should().Contain(
            "prazo: 2027-06-30 → 2027-12-31",
            "valor anterior → novo valor (§14)");
        updated.Detail.Should().Contain("estratégia");
        updated.Detail.Should().Contain("Plano do ciclo");
        updated.Detail.Should().Contain("Plano revisado");
    }

    [Fact]
    public async Task Update_StatusOnlyChange_ShouldNotRequireReason()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        // Mesmos campos, só o status muda (Active → Replanned): a §14
        // não exige motivo para transição de status sozinha (§15 aceite).
        // Status é admin-only (ADR-029) — ator Admin (mantém o gestor).
        var handler = Upsert(
            context, new FakeCurrentUser(Guid.NewGuid(), "Admin"));
        await handler.Handle(
            new UpsertPdiCommand(
                scene.Owner.UserId!.Value, "Plano do ciclo",
                new DateOnly(2027, 6, 30), "Replanned",
                ManagerId: scene.Manager.UserId),
            CancellationToken.None);

        var history = await context.PdiHistoryEntries
            .Where(entry => entry.Type == PdiHistoryType.StatusChanged)
            .OrderByDescending(entry => entry.CreateDateTime)
            .FirstAsync();
        history.FromStatus.Should().Be(PdiStatus.Active);
        history.ToStatus.Should().Be(PdiStatus.Replanned);
    }

    // ---- §15(2) Aprovação de fase ----------------------------------

    [Fact]
    public async Task SubmitPhase_ShouldMoveToPendingValidationAndRecordHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await StartPhaseAsync(context, scene, scene.PhaseId);

        await Submit(context, scene.Owner).Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);

        var phase = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        phase.Status.Should().Be(PdiPhaseStatus.PendingValidation);
        phase.Percent.Should().Be(100, "concluir = 100% (§15)");

        var history = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.PhaseSubmitted);
        history.ActorId.Should().Be(scene.Owner.UserId!.Value);
        history.Detail.Should().Contain("Primeiros passos");
    }

    [Fact]
    public async Task SubmitPhase_WhenNotInProgressOrForeign_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var notStarted = () => Submit(context, scene.Owner).Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);
        await notStarted.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*em execução*");

        var intruder = Submit(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ValidatePhase_ByManager_ShouldCompleteAndStartNextPhase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await StartPhaseAsync(context, scene, scene.PhaseId);
        await Submit(context, scene.Owner).Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);

        await Validate(context, scene.Manager).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Validated),
            CancellationToken.None);

        var first = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        first.Status.Should().Be(PdiPhaseStatus.Completed);

        var next = await context.PdiPhases.SingleAsync(p => p.Id == scene.NextPhaseId);
        next.Status.Should().Be(PdiPhaseStatus.InProgress,
            "validada → próxima fase começa (§15)");

        (await context.PdiHistoryEntries
            .CountAsync(entry => entry.Type == PdiHistoryType.PhaseValidated))
            .Should().Be(1);
        var started = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.PhaseStarted);
        started.Detail.Should().Contain("Segundos passos");
    }

    [Fact]
    public async Task ValidatePhase_Reject_ShouldRequireReasonAndReturnToInProgress()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await StartPhaseAsync(context, scene, scene.PhaseId);
        await Submit(context, scene.Owner).Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);

        var noReason = () => Validate(context, scene.Manager).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Rejected),
            CancellationToken.None);
        await noReason.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*motivo*");

        await Validate(context, scene.Manager).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Rejected,
                "Faltam as evidências do curso"),
            CancellationToken.None);

        var phase = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        phase.Status.Should().Be(PdiPhaseStatus.InProgress);

        var rejected = await context.PdiHistoryEntries
            .SingleAsync(entry => entry.Type == PdiHistoryType.PhaseRejected);
        rejected.Reason.Should().Be("Faltam as evidências do curso");
        rejected.ActorId.Should().Be(scene.Manager.UserId!.Value);
    }

    [Fact]
    public async Task ValidatePhase_ByOwnerOrTwice_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        await StartPhaseAsync(context, scene, scene.PhaseId);
        await Submit(context, scene.Owner).Handle(
            new SubmitPdiPhaseCommand(scene.PhaseId), CancellationToken.None);

        var byOwner = () => Validate(context, scene.Owner).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Validated),
            CancellationToken.None);
        await byOwner.Should().ThrowAsync<UnauthorizedAccessException>();

        await Validate(context, scene.Manager).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Validated),
            CancellationToken.None);

        var again = () => Validate(context, scene.Manager).Handle(
            new ValidatePdiPhaseCommand(
                scene.PhaseId, PdiPhaseValidationDecision.Validated),
            CancellationToken.None);
        await again.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePhase_WhenOwnerBypassesApprovalViaPut_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var ownerPut = PhaseUpdate(context, scene.Owner);

        var toCompleted = () => ownerPut.Handle(
            new UpdatePdiPhaseCommand(
                scene.PhaseId, "Primeiros passos", Status: "Completed",
                Percent: 100),
            CancellationToken.None);
        await toCompleted.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*aprovação*");

        var toPending = () => ownerPut.Handle(
            new UpdatePdiPhaseCommand(
                scene.PhaseId, "Primeiros passos", Status: "PendingValidation"),
            CancellationToken.None);
        await toPending.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*aprovação*");

        var phase = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        phase.Status.Should().Be(PdiPhaseStatus.NotStarted);

        var adminPut = PhaseUpdate(
            context, new FakeCurrentUser(scene.Owner.UserId, "Admin"));
        await adminPut.Handle(
            new UpdatePdiPhaseCommand(
                scene.PhaseId, "Primeiros passos", Status: "Completed",
                Percent: 100),
            CancellationToken.None);
        phase = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        phase.Status.Should().Be(PdiPhaseStatus.Completed);

        // Rebaixar status de governance pelo PUT também é atalho (§15).
        var demote = () => ownerPut.Handle(
            new UpdatePdiPhaseCommand(
                scene.PhaseId, "Primeiros passos", Status: "InProgress"),
            CancellationToken.None);
        await demote.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*aprovação*");
        phase = await context.PdiPhases.SingleAsync(p => p.Id == scene.PhaseId);
        phase.Status.Should().Be(PdiPhaseStatus.Completed);
    }

    // ---- §17 Timeline: check-in e impedimento ----------------------

    [Fact]
    public async Task CheckInAndImpediment_ShouldRecordTimelineEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        await CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                scene.PhaseId, new DateOnly(2026, 10, 7), "Semana produtiva"),
            CancellationToken.None);

        var impediment = await ImpedimentCreate(context, scene.Owner).Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, "Ambiente", "Acesso de VPN bloqueado",
                new DateOnly(2026, 10, 7)),
            CancellationToken.None);

        await ImpedimentUpdate(context, scene.Owner).Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, "Ambiente", "Acesso de VPN bloqueado",
                new DateOnly(2026, 10, 7), Status: "Resolved"),
            CancellationToken.None);

        (await context.PdiHistoryEntries
            .CountAsync(entry => entry.Type == PdiHistoryType.CheckInCreated))
            .Should().Be(1);
        (await context.PdiHistoryEntries
            .CountAsync(entry => entry.Type == PdiHistoryType.ImpedimentCreated))
            .Should().Be(1);
        (await context.PdiHistoryEntries
            .CountAsync(entry => entry.Type == PdiHistoryType.ImpedimentResolved))
            .Should().Be(1);
    }

    // ---- Auditoria de plataforma -----------------------------------

    [Fact]
    public void PdiCommands_ShouldBeAuditableAsPdi()
    {
        ((IAuditableRequest)new UpsertPdiCommand(
            Guid.NewGuid(), "Plano", new DateOnly(2027, 6, 30), "Draft"))
            .AuditResource.Should().Be("PDI");
        ((IAuditableRequest)new SubmitPdiPhaseCommand(Guid.NewGuid()))
            .AuditResource.Should().Be("PDI");
        ((IAuditableRequest)new ValidatePdiPhaseCommand(
            Guid.NewGuid(), PdiPhaseValidationDecision.Validated))
            .AuditResource.Should().Be("PDI");
        ((IAuditableRequest)new CreatePdiCheckInCommand(
            Guid.NewGuid(), new DateOnly(2026, 10, 7), "relato"))
            .AuditResource.Should().Be("PDI");
        ((IAuditableRequest)new UploadPdiEvidenceCommand(
            Guid.NewGuid(), PdiEvidenceType.Document, null, "a.pdf",
            "application/pdf", 1, new MemoryStream([1])))
            .AuditResource.Should().Be("PDI");
    }

    // ---- Fixtures -------------------------------------------------

    private sealed record SceneActors(
        FakeCurrentUser Owner,
        FakeCurrentUser Manager,
        Guid PhaseId,
        Guid NextPhaseId);

    private static async Task<SceneActors> SeedSceneAsync(AppDbContext context)
    {
        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id, manager.Id);

        var ownerActor = new FakeCurrentUser(owner.Id, "Operator");
        var managerActor = new FakeCurrentUser(manager.Id, "Operator");

        var objective = await ObjectiveCreate(context, ownerActor).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Assumir liderança"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, ownerActor).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Formação em gestão"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, ownerActor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Primeiros passos"),
            CancellationToken.None);
        var next = await PhaseCreate(context, ownerActor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Segundos passos"),
            CancellationToken.None);

        return new SceneActors(ownerActor, managerActor, phase.Id, next.Id);
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<IndividualDevelopmentPlan> SeedPlanAsync(
        AppDbContext context, Guid ownerId, Guid? managerId)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId, "Plano do ciclo", new DateOnly(2027, 6, 30),
            PdiStatus.Active, managerId, null, null,
            new PdiChangeActor(ownerId, $"dono-{ownerId}", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        // Persiste o Created como o handler de upsert faria (drain).
        context.PdiHistoryEntries.AddRange(plan.DrainPendingHistory());
        await context.SaveChangesAsync();
        return plan;
    }

    private static async Task StartPhaseAsync(
        AppDbContext context, SceneActors scene, Guid phaseId)
        => await PhaseUpdate(context, scene.Owner).Handle(
            new UpdatePdiPhaseCommand(
                phaseId, phaseId == scene.PhaseId
                    ? "Primeiros passos" : "Segundos passos",
                Status: "InProgress"),
            CancellationToken.None);

    private static UpsertPdiCommandHandler Upsert(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiRepository(context), user,
            NullLogger<UpsertPdiCommandHandler>.Instance);

    private static SubmitPdiPhaseCommandHandler Submit(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<SubmitPdiPhaseCommandHandler>.Instance);

    private static ValidatePdiPhaseCommandHandler Validate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<ValidatePdiPhaseCommandHandler>.Instance);

    private static UpdatePdiPhaseCommandHandler PhaseUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<UpdatePdiPhaseCommandHandler>.Instance);

    private static CreatePdiCheckInCommandHandler CheckInCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiCheckInCommandHandler>.Instance);

    private static CreatePdiImpedimentCommandHandler ImpedimentCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiImpedimentCommandHandler>.Instance);

    private static UpdatePdiImpedimentCommandHandler ImpedimentUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<UpdatePdiImpedimentCommandHandler>.Instance);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiPhaseCommandHandler>.Instance);

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
