using FluentAssertions;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Pdi.Queries;
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
/// Fase 7 do PDI (docs/30 §18, §19, §20, ADR-037): os três dashboards —
/// resumo do colaborador (progresso, fases, próximo prazo, pendências),
/// visão do gestor (classificação atraso/atenção/prazo + agregados) e
/// visão administrativa (contagens globais e taxa de conclusão).
/// </summary>
public sealed class PdiDashboardTests
{
    // ---- §18 Dashboard do colaborador ------------------------------

    [Fact]
    public async Task Dashboard_ForOwner_ShouldSummarizePlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var objective = await ObjectiveCreate(context, scene.Owner).Handle(
            new CreatePdiObjectiveCommand(scene.Owner.Id, "Assumir liderança"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, scene.Owner).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Formação em gestão"),
            CancellationToken.None);

        var phase1 = await PhaseCreate(context, scene.Owner).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Capacitação",
                EndDate: new DateOnly(2027, 3, 1)),
            CancellationToken.None);
        var phase2 = await PhaseCreate(context, scene.Owner).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Aplicação",
                EndDate: new DateOnly(2027, 5, 15)),
            CancellationToken.None);

        // Conclusão exige papel de administrador (guarda da Fase 2/5).
        var admin = new FakeCurrentUser(Guid.NewGuid(), "Admin");
        await PhaseUpdate(context, admin).Handle(
            new UpdatePdiPhaseCommand(phase1.Id, "Capacitação",
                EndDate: new DateOnly(2027, 3, 1),
                Status: nameof(PdiPhaseStatus.Completed), Percent: 100),
            CancellationToken.None);
        await PhaseUpdate(context, scene.Owner).Handle(
            new UpdatePdiPhaseCommand(phase2.Id, "Aplicação",
                EndDate: new DateOnly(2027, 5, 15),
                Status: nameof(PdiPhaseStatus.InProgress), Percent: 60),
            CancellationToken.None);

        await ImpedimentCreate(context, scene.Owner).Handle(
            new CreatePdiImpedimentCommand(
                phase2.Id, "Horário", "Calendário conflitante",
                new DateOnly(2027, 4, 2)),
            CancellationToken.None);
        await CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                phase1.Id, new DateOnly(2027, 2, 10), "Curso concluído"),
            CancellationToken.None);
        await EvidenceUpload(context, scene.Owner, new FakeFileStore()).Handle(
            new UploadPdiEvidenceCommand(
                phase1.Id, PdiEvidenceType.Certificate, "Certificado",
                "certificado.pdf", "application/pdf",
                FileSize: 10, Content: Bytes(10)),
            CancellationToken.None);

        var dto = await DashboardQuery(context, scene.Owner).Handle(
            new GetPdiDashboardQuery(scene.Owner.Id), CancellationToken.None);

        dto.UserId.Should().Be(scene.Owner.Id);
        dto.Status.Should().Be(nameof(PdiStatus.InProgress));
        dto.ProgressPercent.Should().Be(80, "média de 100% e 60% arredondada");
        dto.Phases.Select(phase => phase.Title)
            .Should().Equal("Capacitação", "Aplicação");
        dto.Phases.Select(phase => phase.Status).Should()
            .Equal(nameof(PdiPhaseStatus.Completed),
                nameof(PdiPhaseStatus.InProgress));
        dto.NextDueDate.Should().Be(new DateOnly(2027, 5, 15),
            "fases concluídas não contam como próximo prazo");
        dto.OpenImpediments.Should().Be(1);
        dto.TotalEvidences.Should().Be(1);
        dto.TotalCheckIns.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_ByStranger_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var stranger = new FakeCurrentUser(Guid.NewGuid(), "Operator");

        var act = () => DashboardQuery(context, stranger).Handle(
            new GetPdiDashboardQuery(scene.Owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Dashboard_WithoutPlan_ShouldFail404()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var orphan = await SeedUserAsync(context, "sem-plano@exemplo.test", "Sem");

        var act = () => DashboardQuery(context, scene.Owner).Handle(
            new GetPdiDashboardQuery(orphan.Id), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- §19 Dashboard do gestor ----------------------------------

    [Fact]
    public async Task ManagerDashboard_ShouldClassifyManagedPlans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scene = await SeedSceneAsync(context);

        // A — atrasado (meta vencida, em curso).
        var ownerA = await SeedUserAsync(context, "a@exemplo.test", "Ana Atraso");
        await SeedPlanAsync(context, ownerA.Id, scene.Manager.Id,
            PdiStatus.Active, today.AddDays(-7));

        // B — atenção por impedimento aberto + fase vencendo em 3 dias.
        var ownerB = await SeedUserAsync(context, "b@exemplo.test", "Bia Atenção");
        await SeedPlanAsync(context, ownerB.Id, scene.Manager.Id,
            PdiStatus.Active, today.AddDays(90));
        var phaseB = await SeedPhaseAsync(context, ownerB,
            "Entrega parcial", today.AddDays(3));
        await ImpedimentCreate(context, NewActor(ownerB)).Handle(
            new CreatePdiImpedimentCommand(
                phaseB.Id, "Recurso", "Servidor indisponível",
                today.AddDays(-1)),
            CancellationToken.None);

        // C — atenção por evidência aguardando validação.
        var ownerC = await SeedUserAsync(context, "c@exemplo.test", "Caio Evidência");
        await SeedPlanAsync(context, ownerC.Id, scene.Manager.Id,
            PdiStatus.Active, today.AddDays(90));
        var phaseC = await SeedPhaseAsync(context, ownerC,
            "Apresentação", today.AddDays(60));
        await EvidenceUpload(context, NewActor(ownerC), new FakeFileStore()).Handle(
            new UploadPdiEvidenceCommand(
                phaseC.Id, PdiEvidenceType.Certificate, "Comprovante",
                "comprovante.pdf", "application/pdf",
                FileSize: 4, Content: Bytes(4)),
            CancellationToken.None);

        // D — dentro do prazo, sem pendências.
        var ownerD = await SeedUserAsync(context, "d@exemplo.test", "Davi Ok");
        await SeedPlanAsync(context, ownerD.Id, scene.Manager.Id,
            PdiStatus.InProgress, today.AddDays(90));

        // E — concluído com meta antiga não é "atrasado".
        var ownerE = await SeedUserAsync(context, "e@exemplo.test", "Eva Concluído");
        await SeedPlanAsync(context, ownerE.Id, scene.Manager.Id,
            PdiStatus.Completed, today.AddDays(-30));

        var dto = await ManagerQuery(context, scene.Manager).Handle(
            new GetPdiManagerDashboardQuery(), CancellationToken.None);

        // 5 planos desta cena + o plano do próprio SeedSceneAsync.
        dto.TotalPlans.Should().Be(6);
        dto.Overdue.Should().Be(1);
        dto.NeedsAttention.Should().Be(2);
        dto.OnTrack.Should().Be(3, "planos limpos e o concluído");
        dto.PhasesDueSoon.Should().Be(1, "fase vence em 3 dias (janela de 7)");
        dto.OpenImpediments.Should().Be(1);
        dto.PendingEvidences.Should().Be(1);

        dto.Plans.Should().HaveCount(6);
        var overdue = dto.Plans.Single(plan => plan.UserId == ownerA.Id);
        overdue.Overdue.Should().BeTrue();
        overdue.NeedsAttention.Should().BeFalse();
        overdue.OwnerName.Should().Be("Ana Atraso");
        var attention = dto.Plans.Single(plan => plan.UserId == ownerB.Id);
        attention.NeedsAttention.Should().BeTrue();
        attention.Overdue.Should().BeFalse();
        dto.Plans.Single(plan => plan.UserId == ownerD.Id)
            .NeedsAttention.Should().BeFalse();
    }

    [Fact]
    public async Task ManagerDashboard_WithoutManagedPlans_ShouldReturnZeros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await SeedSceneAsync(context);
        var solo = new FakeCurrentUser(Guid.NewGuid(), "Operator");

        var dto = await ManagerQuery(context, solo).Handle(
            new GetPdiManagerDashboardQuery(), CancellationToken.None);

        dto.TotalPlans.Should().Be(0);
        dto.OnTrack.Should().Be(0);
        dto.NeedsAttention.Should().Be(0);
        dto.Overdue.Should().Be(0);
        dto.PhasesDueSoon.Should().Be(0);
        dto.OpenImpediments.Should().Be(0);
        dto.PendingEvidences.Should().Be(0);
        dto.Plans.Should().BeEmpty();
    }

    // ---- §20 Dashboard administrativo -----------------------------

    [Fact]
    public async Task AdminDashboard_ShouldAggregateAllPlans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var admin = await SeedUserAsync(context, "admin-pdi@exemplo.test", "Ada Admin");
        var adminActor = new FakeCurrentUser(admin.Id, "Admin");

        var ownerCompleted = await SeedUserAsync(
            context, "concluido@exemplo.test", "Concluído");
        var planCompleted = await SeedPlanAsync(
            context, ownerCompleted.Id, null, PdiStatus.Completed,
            new DateOnly(2027, 6, 30));
        var ownerActive = await SeedUserAsync(
            context, "ativo@exemplo.test", "Ativo");
        await SeedPlanAsync(context, ownerActive.Id, null, PdiStatus.InProgress,
            new DateOnly(2027, 6, 30));
        var ownerReplanned = await SeedUserAsync(
            context, "replanejado@exemplo.test", "Replanejado");
        await SeedPlanAsync(context, ownerReplanned.Id, null, PdiStatus.Replanned,
            new DateOnly(2027, 6, 30));
        var ownerCancelled = await SeedUserAsync(
            context, "cancelado@exemplo.test", "Cancelado");
        await SeedPlanAsync(context, ownerCancelled.Id, null, PdiStatus.Cancelled,
            new DateOnly(2027, 6, 30));

        // Acompanhamento semeado no plano concluído.
        var objective = await ObjectiveCreate(context, adminActor).Handle(
            new CreatePdiObjectiveCommand(ownerCompleted.Id, "Meta única"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, adminActor).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Estratégia"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, adminActor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Fase final"),
            CancellationToken.None);
        await PhaseUpdate(context, adminActor).Handle(
            new UpdatePdiPhaseCommand(phase.Id, "Fase final",
                Status: nameof(PdiPhaseStatus.Completed), Percent: 100),
            CancellationToken.None);
        await CheckInCreate(context, adminActor).Handle(
            new CreatePdiCheckInCommand(
                phase.Id, new DateOnly(2027, 1, 15), "Fechamento"),
            CancellationToken.None);
        await ImpedimentCreate(context, adminActor).Handle(
            new CreatePdiImpedimentCommand(
                phase.Id, "Recurso", "Atraso pontual",
                new DateOnly(2027, 1, 5)),
            CancellationToken.None);
        await EvidenceUpload(context, adminActor, new FakeFileStore()).Handle(
            new UploadPdiEvidenceCommand(
                phase.Id, PdiEvidenceType.Certificate, "Certificado",
                "cert.pdf", "application/pdf",
                FileSize: 6, Content: Bytes(6)),
            CancellationToken.None);

        var dto = await AdminQuery(context, adminActor).Handle(
            new GetPdiAdminDashboardQuery(), CancellationToken.None);

        dto.TotalPlans.Should().Be(4);
        dto.CompletedPlans.Should().Be(1);
        dto.InProgressPlans.Should().Be(1);
        dto.ReplannedPlans.Should().Be(1);
        dto.CancelledPlans.Should().Be(1);
        dto.CompletionRatePercent.Should().Be(25, "1 concluído em 4");
        dto.CompletedPhases.Should().Be(1);
        dto.TotalCheckIns.Should().Be(1);
        dto.TotalEvidences.Should().Be(1);
        dto.TotalImpediments.Should().Be(1);
    }

    [Fact]
    public async Task AdminDashboard_ByNonAdmin_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var act = () => AdminQuery(context, scene.Owner).Handle(
            new GetPdiAdminDashboardQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---- Fixtures -------------------------------------------------

    private sealed record SceneActors(FakeCurrentUser Owner, FakeCurrentUser Manager);

    private static async Task<SceneActors> SeedSceneAsync(AppDbContext context)
    {
        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id, manager.Id, PdiStatus.InProgress,
            new DateOnly(2027, 6, 30));
        return new SceneActors(NewActor(owner), NewActor(manager));
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
        AppDbContext context, Guid ownerId, Guid? managerId,
        PdiStatus status, DateOnly targetDate)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId, "Plano do ciclo", targetDate, status, managerId, null, null,
            new PdiChangeActor(ownerId, $"dono-{ownerId}", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    private static async Task<PdiPhaseNodeDto> SeedPhaseAsync(
        AppDbContext context, User owner, string title, DateOnly endDate)
    {
        var actor = NewActor(owner);
        var objective = await ObjectiveCreate(context, actor).Handle(
            new CreatePdiObjectiveCommand(owner.Id, $"Objetivo de {owner.Email}"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, actor).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Estratégia"),
            CancellationToken.None);
        return await PhaseCreate(context, actor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, title, EndDate: endDate),
            CancellationToken.None);
    }

    private static Stream Bytes(int count)
        => new MemoryStream(new byte[count]);

    private static FakeCurrentUser NewActor(User user)
        => new(user.Id, "Operator");

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static GetPdiDashboardQueryHandler DashboardQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiRepository(context), new PdiStructureRepository(context), user);

    private static GetPdiManagerDashboardQueryHandler ManagerQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiRepository(context), new PdiStructureRepository(context), user);

    private static GetPdiAdminDashboardQueryHandler AdminQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiRepository(context), new PdiStructureRepository(context), user);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiPhaseCommandHandler>.Instance);

    private static UpdatePdiPhaseCommandHandler PhaseUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<UpdatePdiPhaseCommandHandler>.Instance);

    private static CreatePdiCheckInCommandHandler CheckInCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiCheckInCommandHandler>.Instance);

    private static CreatePdiImpedimentCommandHandler ImpedimentCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiImpedimentCommandHandler>.Instance);

    private static UploadPdiEvidenceCommandHandler EvidenceUpload(
        AppDbContext context, FakeCurrentUser user, FakeFileStore store)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, store, NullLogger<UploadPdiEvidenceCommandHandler>.Instance);

    private sealed class FakeFileStore : IPdiEvidenceFileStore
    {
        public Task SaveAsync(
            Guid evidenceId, string extension, Stream content,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

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
