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
/// PDI (docs/29 §8) — regra do usuário 2026-10-03/04: Admin gerencia todo
/// o CRUD, Operador edita só o próprio (criação = Admin), eventos com
/// ActorId para o consumer decidir destinatário/canais.
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
    public async Task Upsert_WhenOperatorEditsOwnExisting_ShouldUpdateOnceAndRaiseUpdatedEvent()
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

        var operatorUser = new FakeCurrentUser(owner.Id, "Operator");
        await UpsertHandler(context, operatorUser, ["Operator"]).Handle(
            new UpsertPdiCommand(
                owner.Id, "Plano revisado pelo dono", new DateOnly(2027, 1, 31), "Completed"),
            CancellationToken.None);

        var plans = await context.IndividualDevelopmentPlans.ToListAsync();
        plans.Should().ContainSingle("1 PDI vigente por usuário (upsert)");

        var plan = plans.Single();
        plan.Strategy.Should().Be("Plano revisado pelo dono");
        plan.Status.Should().Be(PdiStatus.Completed);

        var updated = plan.DomainEvents.OfType<PdiUpdatedDomainEvent>().Should().ContainSingle()
            .Which;
        updated.ActorId.Should().Be(owner.Id);
        updated.OwnerEmail.Should().Be("operador@exemplo.test");
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
                new UpsertPdiCommand(user.Id, $"Plano de {user.Email}", new DateOnly(2026, 12, 31), "InProgress"),
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
