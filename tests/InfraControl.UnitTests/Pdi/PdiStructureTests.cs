using FluentAssertions;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
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
/// Fase 2 do PDI (docs/30 §5-§8, ADR-032): catálogo de competências
/// (Admin), árvore objetivo → estratégia → fase → atividade com prazos,
/// escopo dono/Admin (403), exclusão com children → 409, validações
/// de fase (status/percentual/período/responsável) → 400 pt-BR.
/// </summary>
public sealed class PdiStructureTests
{
    [Fact]
    public async Task Competency_CreateAndDuplicate_ShouldPersistThen409()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var created = await CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("Liderança", "Competência de liderar times"),
            CancellationToken.None);

        created.Name.Should().Be("Liderança");
        created.Description.Should().Be("Competência de liderar times");

        var duplicate = () => CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("  liderança  "),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Já existe*");

        (await context.PdiCompetencies.ToListAsync())
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Competency_UpdateDuplicateOrDeleteInUse_Should409()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var lideranca = await CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("Liderança"), CancellationToken.None);
        var comunicacao = await CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("Comunicação"), CancellationToken.None);

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        await ObjectiveCreate(context, new FakeCurrentUser(owner.Id, "Operator"))
            .Handle(new CreatePdiObjectiveCommand(
                owner.Id, "Assumir liderança de equipe", CompetencyId: lideranca.Id),
                CancellationToken.None);

        // Em uso → 409.
        var deleteInUse = () => CompetencyDelete(context).Handle(
            new DeletePdiCompetencyCommand(lideranca.Id), CancellationToken.None);
        await deleteInUse.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*objetivos vinculados*");

        // Renomear para nome de outra → 409.
        var renameToDuplicate = () => CompetencyUpdate(context).Handle(
            new UpdatePdiCompetencyCommand(comunicacao.Id, "liderança"),
            CancellationToken.None);
        await renameToDuplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Já existe*");

        // Livre → remove.
        await CompetencyDelete(context).Handle(
            new DeletePdiCompetencyCommand(comunicacao.Id), CancellationToken.None);
        (await context.PdiCompetencies.ToListAsync())
            .Should().ContainSingle(item => item.Id == lideranca.Id);
    }

    [Fact]
    public async Task Objective_Create_ShouldPersistWithCompetencyAndOrders()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);

        var competency = await CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("Liderança"), CancellationToken.None);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        var handler = ObjectiveCreate(context, operatorUser);

        var first = await handler.Handle(
            new CreatePdiObjectiveCommand(
                owner.Id, "Assumir liderança de equipe",
                Description: "Liderar o time de infraestrutura",
                CompetencyId: competency.Id,
                CurrentSituation: "Sem liderança formal",
                DesiredSituation: "Líder de squad",
                ExpectedResult: "Time com rituais estabelecidos",
                Indicator: "Equipe satisfeita (survey)",
                TargetDate: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        var second = await handler.Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Reduzir MTTR"),
            CancellationToken.None);

        first.CompetencyName.Should().Be("Liderança");
        first.Order.Should().Be(0);
        second.Order.Should().Be(1, "criação anexa ao fim da lista");
        second.CompetencyId.Should().BeNull();

        var objectives = await context.PdiObjectives.ToListAsync();
        objectives.Should().HaveCount(2);
        objectives.Should().OnlyContain(item => item.UserId == owner.Id);
    }

    [Fact]
    public async Task Objective_Create_WhenPlanMissingOrUnknownCompetencyOrForeignOperator_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        var handler = ObjectiveCreate(context, operatorUser);

        var noPlan = () => handler.Handle(
            new CreatePdiObjectiveCommand(Guid.NewGuid(), "Órfão"),
            CancellationToken.None);
        await noPlan.Should().ThrowAsync<KeyNotFoundException>();

        var unknownCompetency = () => handler.Handle(
            new CreatePdiObjectiveCommand(
                owner.Id, "Com competência inválida", CompetencyId: Guid.NewGuid()),
            CancellationToken.None);
        await unknownCompetency.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Competência*");

        var intruder = ObjectiveCreate(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreignPlan = () => intruder.Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Plano capturado"),
            CancellationToken.None);
        await foreignPlan.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");

        (await context.PdiObjectives.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Objective_Delete_WhenStrategiesExist_Should409_WhenEmpty_ShouldRemove()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var empty = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Objetivo vazio"),
            CancellationToken.None);
        var withStrategy = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Objetivo com estratégia"),
            CancellationToken.None);
        await StrategyCreate(context, operatorUser).Handle(
            new CreatePdiStrategyCommand(withStrategy.Id, "Fazer curso"),
            CancellationToken.None);

        var deleteBlocked = () => ObjectiveDelete(context, operatorUser).Handle(
            new DeletePdiObjectiveCommand(withStrategy.Id), CancellationToken.None);
        await deleteBlocked.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*estratégias vinculadas*");

        await ObjectiveDelete(context, operatorUser).Handle(
            new DeletePdiObjectiveCommand(empty.Id), CancellationToken.None);
        (await context.PdiObjectives.ToListAsync())
            .Should().ContainSingle(item => item.Id == withStrategy.Id);
    }

    [Fact]
    public async Task FullChain_Create_ShouldLinkObjectiveStrategyPhaseActivity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var objective = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Assumir liderança"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, operatorUser).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Formação em gestão"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, operatorUser).Handle(
            new CreatePdiPhaseCommand(
                strategy.Id, "Primeiros passos",
                Description: "Fundamentos de gestão",
                ResponsibleId: manager.Id,
                StartDate: new DateOnly(2026, 10, 1),
                EndDate: new DateOnly(2026, 12, 15),
                CompletionCriteria: "Aprovação no módulo 1",
                Status: "inprogress",
                Percent: 40),
            CancellationToken.None);
        var activity = await ActivityCreate(context, operatorUser).Handle(
            new CreatePdiActivityCommand(
                phase.Id, "Realizar curso de liderança",
                DueDate: new DateOnly(2026, 11, 30)),
            CancellationToken.None);

        phase.Status.Should().Be("InProgress", "status normalizado (case-insensitive)");
        phase.Percent.Should().Be(40);
        phase.ResponsibleName.Should().Be("Carla Gestor");
        phase.Order.Should().Be(0);
        activity.Order.Should().Be(0);

        (await context.PdiStrategies.SingleAsync()).ObjectiveId.Should().Be(objective.Id);
        (await context.PdiPhases.SingleAsync()).StrategyId.Should().Be(strategy.Id);
        (await context.PdiActivities.SingleAsync()).PhaseId.Should().Be(phase.Id);
    }

    [Fact]
    public async Task Phase_Create_WhenInvalidInput_ShouldThrow400PtBr()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var objective = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Objetivo"), CancellationToken.None);
        var strategy = await StrategyCreate(context, operatorUser).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Estratégia"), CancellationToken.None);
        var handler = PhaseCreate(context, operatorUser);

        var badStatus = () => handler.Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Fase", Status: "Qualquer"),
            CancellationToken.None);
        await badStatus.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Status*");

        var badPercent = () => handler.Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Fase", Percent: 101),
            CancellationToken.None);
        await badPercent.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*percentual*");

        var invertedDates = () => handler.Handle(
            new CreatePdiPhaseCommand(
                strategy.Id, "Fase",
                StartDate: new DateOnly(2026, 12, 1),
                EndDate: new DateOnly(2026, 1, 1)),
            CancellationToken.None);
        await invertedDates.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*fim da fase*");

        var unknownResponsible = () => handler.Handle(
            new CreatePdiPhaseCommand(
                strategy.Id, "Fase", ResponsibleId: Guid.NewGuid()),
            CancellationToken.None);
        await unknownResponsible.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Responsável*");

        (await context.PdiPhases.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StrategyOrPhase_Delete_WhenChildrenExist_Should409()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var objective = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Objetivo"), CancellationToken.None);
        var strategy = await StrategyCreate(context, operatorUser).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Estratégia"), CancellationToken.None);
        var phase = await PhaseCreate(context, operatorUser).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Fase"), CancellationToken.None);
        await ActivityCreate(context, operatorUser).Handle(
            new CreatePdiActivityCommand(phase.Id, "Atividade"), CancellationToken.None);

        var strategyBlocked = () => StrategyDelete(context, operatorUser).Handle(
            new DeletePdiStrategyCommand(strategy.Id), CancellationToken.None);
        await strategyBlocked.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fases vinculadas*");

        var phaseBlocked = () => PhaseDelete(context, operatorUser).Handle(
            new DeletePdiPhaseCommand(phase.Id), CancellationToken.None);
        await phaseBlocked.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*atividades vinculadas*");

        // Remove a atividade e a fase passa a ser removível.
        await ActivityDelete(context, operatorUser).Handle(
            new DeletePdiActivityCommand(
                (await context.PdiActivities.SingleAsync()).Id),
            CancellationToken.None);
        await PhaseDelete(context, operatorUser).Handle(
            new DeletePdiPhaseCommand(phase.Id), CancellationToken.None);
        (await context.PdiPhases.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ActivityUpdate_ShouldToggleCompletedOnOwnPlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var activity = await FullActivityAsync(context, operatorUser);

        await ActivityUpdate(context, operatorUser).Handle(
            new UpdatePdiActivityCommand(
                activity.Id, "Realizar curso de liderança",
                DueDate: new DateOnly(2026, 11, 30), Completed: true),
            CancellationToken.None);

        var stored = await context.PdiActivities.SingleAsync();
        stored.Completed.Should().BeTrue();
        stored.Title.Should().Be("Realizar curso de liderança");

        var intruder = ActivityUpdate(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new UpdatePdiActivityCommand(activity.Id, "Capturada"),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");
        (await context.PdiActivities.SingleAsync()).Title
            .Should().Be("Realizar curso de liderança");
    }

    [Fact]
    public async Task StructureQuery_ShouldGroupTreeResolveNamesAndOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id);
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");

        var competency = await CompetencyCreate(context).Handle(
            new CreatePdiCompetencyCommand("Liderança"), CancellationToken.None);

        var first = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(
                owner.Id, "Primeiro objetivo", CompetencyId: competency.Id),
            CancellationToken.None);
        var second = await ObjectiveCreate(context, operatorUser).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Segundo objetivo"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, operatorUser).Handle(
            new CreatePdiStrategyCommand(first.Id, "Formação em gestão"),
            CancellationToken.None);
        await PhaseCreate(context, operatorUser).Handle(
            new CreatePdiPhaseCommand(
                strategy.Id, "Primeiros passos", ResponsibleId: manager.Id),
            CancellationToken.None);
        await ActivityCreate(context, operatorUser).Handle(
            new CreatePdiActivityCommand(
                (await context.PdiPhases.SingleAsync()).Id, "Curso"),
            CancellationToken.None);

        // Admin lê o PDI de qualquer usuário (ADR-030 matrix).
        var admin = await StructureQuery(context, new FakeCurrentUser(Guid.NewGuid(), "Admin"))
            .Handle(new GetPdiStructureQuery(owner.Id), CancellationToken.None);

        admin.UserId.Should().Be(owner.Id);
        admin.Objectives.Should().HaveCount(2, "ordenadas pela posição de criação");
        admin.Objectives[0].Id.Should().Be(first.Id);
        admin.Objectives[0].CompetencyName.Should().Be("Liderança");
        admin.Objectives[1].CompetencyName.Should().BeNull();

        var nested = admin.Objectives[0].Strategies.Should().ContainSingle().Which;
        nested.Title.Should().Be("Formação em gestão");
        var phase = nested.Phases.Should().ContainSingle().Which;
        phase.ResponsibleName.Should().Be("Carla Gestor");
        phase.Activities.Should().ContainSingle()
            .Which.Title.Should().Be("Curso");

        second.Strategies.Should().BeEmpty("objetivo sem estratégias");
    }

    [Fact]
    public async Task StructureQuery_WhenOperatorReadsOtherOrNoPlan_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await SeedPlanAsync(context, owner.Id);

        var intruder = StructureQuery(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new GetPdiStructureQuery(owner.Id), CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");

        var withoutPlan = await SeedUserAsync(context, "sem-plano@exemplo.test", "Sem");
        var missingPlan = StructureQuery(context, new FakeCurrentUser(withoutPlan.Id, "Operator"));
        var absent = () => missingPlan.Handle(
            new GetPdiStructureQuery(withoutPlan.Id), CancellationToken.None);
        await absent.Should().ThrowAsync<KeyNotFoundException>();

        var own = StructureQuery(context, new FakeCurrentUser(owner.Id, "Operator"));
        var structure = await own.Handle(
            new GetPdiStructureQuery(owner.Id), CancellationToken.None);
        structure.Objectives.Should().BeEmpty("PDI sem estrutura começa vazia");
    }

    // ---- Fixtures -------------------------------------------------

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
        AppDbContext context, Guid ownerId)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId, "Plano do ciclo", new DateOnly(2027, 6, 30),
            PdiStatus.Active, null, null, null,
            new PdiChangeActor(ownerId, $"dono-{ownerId}", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    private static async Task<PdiActivityNodeDto> FullActivityAsync(
        AppDbContext context, FakeCurrentUser user)
    {
        var objective = await ObjectiveCreate(context, user).Handle(
            new CreatePdiObjectiveCommand(user.UserId!.Value, "Objetivo"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, user).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Estratégia"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, user).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Fase"), CancellationToken.None);
        return await ActivityCreate(context, user).Handle(
            new CreatePdiActivityCommand(phase.Id, "Atividade"), CancellationToken.None);
    }

    private static CreatePdiCompetencyCommandHandler CompetencyCreate(AppDbContext context)
        => new(new PdiStructureRepository(context),
            NullLogger<CreatePdiCompetencyCommandHandler>.Instance);

    private static UpdatePdiCompetencyCommandHandler CompetencyUpdate(AppDbContext context)
        => new(new PdiStructureRepository(context),
            NullLogger<UpdatePdiCompetencyCommandHandler>.Instance);

    private static DeletePdiCompetencyCommandHandler CompetencyDelete(AppDbContext context)
        => new(new PdiStructureRepository(context),
            NullLogger<DeletePdiCompetencyCommandHandler>.Instance);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static DeletePdiObjectiveCommandHandler ObjectiveDelete(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<DeletePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static DeletePdiStrategyCommandHandler StrategyDelete(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<DeletePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiPhaseCommandHandler>.Instance);

    private static DeletePdiPhaseCommandHandler PhaseDelete(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<DeletePdiPhaseCommandHandler>.Instance);

    private static CreatePdiActivityCommandHandler ActivityCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiActivityCommandHandler>.Instance);

    private static UpdatePdiActivityCommandHandler ActivityUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<UpdatePdiActivityCommandHandler>.Instance);

    private static DeletePdiActivityCommandHandler ActivityDelete(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<DeletePdiActivityCommandHandler>.Instance);

    private static GetPdiStructureQueryHandler StructureQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user);

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => $"ator-{UserId}@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
