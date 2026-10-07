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
/// PDI (docs/29 §8, docs/30, ADR-029) — regras 2026-10-03/04: Admin
/// gerencia todo o CRUD (criação = Admin), Operador edita só o próprio
/// e só o aceite muda status; ciclo de 9 estados com transições da
/// tabela docs/30 §3; gestor/período definidos só pelo Admin; histórico
/// (timeline) persistido no mesmo save. Eventos com ActorId para o
/// consumer decidir destinatário/canais.
/// </summary>
public sealed class PdiCommandTests
{
    [Fact]
    public async Task Create_WhenAdmin_ShouldPersistAndRaiseCreatedEventWithActor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var admin = SeedUserIds.New();

        var dto = await UpsertHandler(context, admin, ["Admin"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Dominar monitoramento e automação de rede", new DateOnly(2026, 12, 31), "InProgress"),
            CancellationToken.None);

        dto.OwnerName.Should().Be("Ana Dono");
        dto.OwnerEmail.Should().Be("dono@exemplo.test");
        dto.Status.Should().Be("InProgress");

        var plan = context.IndividualDevelopmentPlans.Local.Should().ContainSingle()
            .Which;
        plan.UserId.Should().Be(owner.Id);

        var created = plan.DomainEvents.OfType<PdiCreatedDomainEvent>().Should().ContainSingle()
            .Which;
        created.ActorId.Should().Be(admin.UserId!.Value);
        created.ActorEmail.Should().Be(admin.UserName);
        created.OwnerEmail.Should().Be("dono@exemplo.test");
        created.TargetDate.Should().Be(new DateOnly(2026, 12, 31));

        var entry = await context.PdiHistoryEntries.SingleAsync();
        entry.Type.Should().Be(PdiHistoryType.Created);
        entry.ToStatus.Should().Be(PdiStatus.InProgress);
        entry.ActorId.Should().Be(admin.UserId!.Value);
    }

    [Fact]
    public async Task Create_WhenOperator_ShouldForbid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        // Alvo = o próprio operador (existente): ownership passa e cai na
        // regra "criação é exclusiva do Admin".
        var operatorEntity = await SeedUserAsync(context, "operador@exemplo.test", "Bob Operador");
        var operatorUser = new FakeCurrentUser(operatorEntity.Id, "Operator");
        var act = () => UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(
                operatorEntity.Id,
                "Estratégia qualquer", new DateOnly(2026, 12, 31), "InProgress"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*administrador*");
        context.IndividualDevelopmentPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Upsert_WhenOperatorEditsOtherUser_ShouldForbid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano inicial", new DateOnly(2026, 11, 30), "InProgress"),
            CancellationToken.None);

        var intruder = SeedUserIds.New();
        var act = () => UpsertHandler(context, intruder, ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano capturado", new DateOnly(2026, 11, 30), "InProgress"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");
        (await context.IndividualDevelopmentPlans.SingleAsync()).Strategy
            .Should().Be("Plano inicial");
    }

    [Fact]
    public async Task Upsert_WhenOperatorEditsOwnExisting_ShouldUpdateFieldsAndKeepStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "operador@exemplo.test", "Bob Operador");
        await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano inicial", new DateOnly(2026, 11, 30), "InProgress"),
            CancellationToken.None);

        // Operador sem transição de status: só campos (decisão 2026-10-04).
        // §14 (Fase 5): mudar campos exige motivo.
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        await UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano revisado pelo dono", new DateOnly(2027, 1, 31),
                "InProgress", Reason: "Revisão do dono"),
            CancellationToken.None);

        var plans = await context.IndividualDevelopmentPlans.ToListAsync();
        plans.Should().ContainSingle("1 PDI vigente por usuário (upsert)");

        var plan = plans.Single();
        plan.Strategy.Should().Be("Plano revisado pelo dono");
        plan.TargetDate.Should().Be(new DateOnly(2027, 1, 31));
        plan.Status.Should().Be(PdiStatus.InProgress);

        var updated = plan.DomainEvents.OfType<PdiUpdatedDomainEvent>().Should().ContainSingle()
            .Which;
        updated.ActorId.Should().Be(owner.Id);
        updated.OwnerEmail.Should().Be("operador@exemplo.test");

        var entry = (await context.PdiHistoryEntries
            .Where(item => item.Type == PdiHistoryType.Updated)
            .ToArrayAsync()).Should().ContainSingle().Which;
        entry.Detail.Should().Be(
            "estratégia: \"Plano inicial\" → \"Plano revisado pelo dono\"; " +
            "prazo: 2026-11-30 → 2027-01-31");
        entry.Reason.Should().Be("Revisão do dono");
        entry.ActorId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task Upsert_WhenUserUnknown_ShouldThrowNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var act = () => UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(
                Guid.NewGuid(), "Plano órfão", new DateOnly(2026, 12, 31), "InProgress"),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Upsert_WhenInvalidInput_ShouldThrowArgument()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var admin = SeedUserIds.New();
        var handler = UpsertHandler(context, admin, ["Admin"]);

        var badStatus = () => handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", new DateOnly(2026, 12, 31), "Qualquer"),
            CancellationToken.None);
        await badStatus.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Status*");

        var emptyStrategy = () => handler.Handle(
            new UpsertPdiCommand(owner.Id, "   ", new DateOnly(2026, 12, 31), "InProgress"),
            CancellationToken.None);
        await emptyStrategy.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*estratégia*");

        var defaultDate = () => handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", default, "InProgress"),
            CancellationToken.None);
        await defaultDate.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*prazo*");

        var invertedPeriod = () => handler.Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano", new DateOnly(2026, 12, 31), "InProgress",
                ManagerId: null,
                PeriodStart: new DateOnly(2026, 12, 1),
                PeriodEnd: new DateOnly(2026, 1, 1)),
            CancellationToken.None);
        await invertedPeriod.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*período*");
    }

    [Fact]
    public async Task List_ShouldScopeByRole()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var alice = await SeedUserAsync(context, "alice@exemplo.test", "Alice");
        var bob = await SeedUserAsync(context, "bob@exemplo.test", "Bob");
        var admin = SeedUserIds.New();
        var adminHandler = UpsertHandler(context, admin, ["Admin"]);

        foreach (var user in new[] { alice, bob })
            await adminHandler.Handle(
                new UpsertPdiCommand(user.Id, $"Plano de {user.Email}", new DateOnly(2026, 12, 31), "Draft"),
                CancellationToken.None);

        var asAdmin = await ListHandler(context, admin.UserId, ["Admin"]).Handle(
            new ListPdisQuery(), CancellationToken.None);
        asAdmin.Should().HaveCount(2);

        var asAlice = await ListHandler(context, alice.Id, ["Operator"]).Handle(
            new ListPdisQuery(), CancellationToken.None);
        asAlice.Should().ContainSingle()
            .Which.UserId.Should().Be(alice.Id);

        var withoutPlan = await SeedUserAsync(context, "sem-plano@exemplo.test", "Sem");
        var asOther = await ListHandler(context, withoutPlan.Id, ["Operator"]).Handle(
            new ListPdisQuery(), CancellationToken.None);
        asOther.Should().BeEmpty();
    }

    [Fact]
    public async Task Transitions_ShouldFollowFullLifecycleTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var admin = SeedUserIds.New();
        var handler = UpsertHandler(context, admin, ["Admin"]);
        var target = new DateOnly(2027, 6, 30);

        // Ciclo completo docs/30 §3: Draft → … → Completed.
        await handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano do ciclo", target, "Draft"),
            CancellationToken.None);

        foreach (var next in new[]
                 {
                     "Planned", "AwaitingAcceptance", "Active",
                     "InProgress", "UnderEvaluation", "Completed"
                 })
            await handler.Handle(
                new UpsertPdiCommand(owner.Id, "Plano do ciclo", target, next),
                CancellationToken.None);

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.Status.Should().Be(PdiStatus.Completed);

        var history = await context.PdiHistoryEntries.ToArrayAsync();
        history.Should().HaveCount(7, "1 Created + 6 transições");
        history.Count(entry => entry.Type == PdiHistoryType.Created)
            .Should().Be(1);
        history.Count(entry => entry.Type == PdiHistoryType.StatusChanged)
            .Should().Be(6);
        history.Should().OnlyContain(entry => entry.UserId == owner.Id);
    }

    [Fact]
    public async Task Transitions_WhenSkippingStep_ShouldThrow400AndKeepState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var handler = UpsertHandler(context, SeedUserIds.New(), ["Admin"]);
        var target = new DateOnly(2027, 6, 30);

        await handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", target, "Draft"),
            CancellationToken.None);

        var skip = () => handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", target, "Completed"),
            CancellationToken.None);
        await skip.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Transição de status inválida*");

        (await context.IndividualDevelopmentPlans.SingleAsync()).Status
            .Should().Be(PdiStatus.Draft, "estado intacto após 400");

        var cancelledFromDraft = () => handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", target, "Cancelled"),
            CancellationToken.None);
        await cancelledFromDraft.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Transição de status inválida*");
    }

    [Fact]
    public async Task Transitions_WhenCancelledFromActive_ShouldRecordReason()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var handler = UpsertHandler(context, SeedUserIds.New(), ["Admin"]);
        var target = new DateOnly(2027, 6, 30);

        await handler.Handle(
            new UpsertPdiCommand(owner.Id, "Plano", target, "Draft"), CancellationToken.None);
        foreach (var next in new[] { "Planned", "AwaitingAcceptance", "Active" })
            await handler.Handle(
                new UpsertPdiCommand(owner.Id, "Plano", target, next), CancellationToken.None);

        await handler.Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano", target, "Cancelled", Reason: "Encerrado pelo gestor"),
            CancellationToken.None);

        (await context.IndividualDevelopmentPlans.SingleAsync()).Status
            .Should().Be(PdiStatus.Cancelled);

        var cancelled = (await context.PdiHistoryEntries
                .Where(entry => entry.ToStatus == PdiStatus.Cancelled)
                .ToArrayAsync())
            .Should().ContainSingle().Which;
        cancelled.FromStatus.Should().Be(PdiStatus.Active);
        cancelled.Reason.Should().Be("Encerrado pelo gestor");
    }

    [Fact]
    public async Task Upsert_WhenOperatorTriesStatusChange_ShouldForbid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "operador@exemplo.test", "Bob Operador");
        await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(owner.Id, "Plano", new DateOnly(2026, 12, 31), "Planned"),
            CancellationToken.None);

        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        var act = () => UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(owner.Id, "Plano", new DateOnly(2026, 12, 31), "Active"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*administrador*");
        (await context.IndividualDevelopmentPlans.SingleAsync()).Status
            .Should().Be(PdiStatus.Planned);
    }

    [Fact]
    public async Task Upsert_WhenOperatorAcceptsAwaiting_ShouldAllowWithReason()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "operador@exemplo.test", "Bob Operador");
        await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano", new DateOnly(2026, 12, 31), "AwaitingAcceptance"),
            CancellationToken.None);

        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        var dto = await UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano", new DateOnly(2026, 12, 31), "Active",
                Reason: "Aceito pelo dono"),
            CancellationToken.None);

        dto.Status.Should().Be("Active");
        (await context.IndividualDevelopmentPlans.SingleAsync()).Status
            .Should().Be(PdiStatus.Active);

        var accepted = (await context.PdiHistoryEntries
                .Where(entry => entry.Type == PdiHistoryType.StatusChanged)
                .ToArrayAsync())
            .Should().ContainSingle().Which;
        accepted.FromStatus.Should().Be(PdiStatus.AwaitingAcceptance);
        accepted.ToStatus.Should().Be(PdiStatus.Active);
        accepted.Reason.Should().Be("Aceito pelo dono");
        accepted.ActorId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task Upsert_WhenAdminSetsManagerAndPeriod_OperatorEditShouldKeepThem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        var other = await SeedUserAsync(context, "outro@exemplo.test", "Outro Líder");

        var created = await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano com gestor", new DateOnly(2026, 12, 31), "Active",
                ManagerId: manager.Id,
                PeriodStart: new DateOnly(2026, 7, 1),
                PeriodEnd: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        created.ManagerId.Should().Be(manager.Id);
        created.ManagerName.Should().Be("Carla Gestor");
        created.PeriodStart.Should().Be(new DateOnly(2026, 7, 1));
        created.PeriodEnd.Should().Be(new DateOnly(2026, 12, 31));

        // Operador manda gestor/período diferentes: entrada ignorada
        // (ADR-029 §3) — só a estratégia muda (§14: com motivo).
        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        var edited = await UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Estratégia revisada", new DateOnly(2026, 12, 31), "Active",
                ManagerId: other.Id,
                PeriodStart: new DateOnly(2027, 1, 1),
                PeriodEnd: new DateOnly(2027, 6, 30),
                Reason: "Revisão do dono"),
            CancellationToken.None);

        var plan = await context.IndividualDevelopmentPlans.SingleAsync();
        plan.Strategy.Should().Be("Estratégia revisada");
        plan.ManagerId.Should().Be(manager.Id, "gestor só é definido pelo Admin");
        plan.PeriodStart.Should().Be(new DateOnly(2026, 7, 1));
        plan.PeriodEnd.Should().Be(new DateOnly(2026, 12, 31));
        edited.ManagerName.Should().Be("Carla Gestor");
    }

    [Fact]
    public async Task History_ShouldReturnNewestFirstWithResolvedActorNames()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var adminEntity = await SeedUserAsync(context, "chefe@exemplo.test", "Zeca Admin");
        var admin = new FakeCurrentUser(adminEntity.Id, "Admin");
        var target = new DateOnly(2027, 6, 30);

        await UpsertHandler(context, admin, ["Admin"]).Handle(
            new UpsertPdiCommand(owner.Id, "Plano v1", target, "Draft"), CancellationToken.None);
        await UpsertHandler(context, admin, ["Admin"]).Handle(
            new UpsertPdiCommand(owner.Id, "Plano v1", target, "Planned"), CancellationToken.None);
        // §14 (Fase 5): troca de estratégia exige motivo.
        await UpsertHandler(context, new FakeCurrentUser(owner.Id, "Operator"), ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano v2", target, "Planned", Reason: "Nova direção"),
            CancellationToken.None);

        var history = (await HistoryHandler(context, admin.UserId, ["Admin"]).Handle(
                new GetPdiHistoryQuery(owner.Id), CancellationToken.None))
            .ToArray();

        history.Should().HaveCount(3);
        history.Select(entry => entry.Type)
            .Should().Equal("Updated", "StatusChanged", "Created");

        history[0].Detail.Should().Be(
            "estratégia: \"Plano v1\" → \"Plano v2\"");
        history[0].ActorName.Should().Be("Ana Dono");

        history[1].FromStatus.Should().Be("Draft");
        history[1].ToStatus.Should().Be("Planned");
        history[1].ActorName.Should().Be("Zeca Admin");

        history[2].ToStatus.Should().Be("Draft");
        history[2].ActorName.Should().Be("Zeca Admin");

        history.Should().BeInDescendingOrder(entry => entry.OccurredAt);
    }

    [Fact]
    public async Task History_WhenOperatorReadsOtherUser_ShouldForbid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        await UpsertHandler(context, SeedUserIds.New(), ["Admin"]).Handle(
            new UpsertPdiCommand(owner.Id, "Plano", new DateOnly(2026, 12, 31), "Draft"),
            CancellationToken.None);

        var intruder = SeedUserIds.New();
        var act = () => HistoryHandler(context, intruder.UserId, ["Operator"]).Handle(
            new GetPdiHistoryQuery(owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");

        var own = await HistoryHandler(context, owner.Id, ["Operator"]).Handle(
            new GetPdiHistoryQuery(owner.Id), CancellationToken.None);
        own.Should().ContainSingle()
            .Which.Type.Should().Be("Created");
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static UpsertPdiCommandHandler UpsertHandler(
        AppDbContext context, FakeCurrentUser user, params string[] roles)
        => new(
            new PdiRepository(context),
            roles.Length > 0 ? new FakeCurrentUser(user.UserId, roles) : user,
            NullLogger<UpsertPdiCommandHandler>.Instance);

    private static ListPdisQueryHandler ListHandler(
        AppDbContext context, Guid? userId, params string[] roles)
        => new(new PdiRepository(context), new FakeCurrentUser(userId, roles));

    private static GetPdiHistoryQueryHandler HistoryHandler(
        AppDbContext context, Guid? userId, params string[] roles)
        => new(new PdiRepository(context), new FakeCurrentUser(userId, roles));

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static class SeedUserIds
    {
        public static FakeCurrentUser New()
            => new(Guid.NewGuid(), "Admin");
    }

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => $"ator-{UserId}@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
