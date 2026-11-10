using FluentAssertions;
using InfraControl.Application.Common.Audit.Queries;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using InfraControl.Infrastructure.Services;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class AuditQueryTests
{
    [Fact]
    public async Task Search_ShouldFilterByActionAndOutcome()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        await context.AuditEntries.AddRangeAsync(
            new AuditEntry("CreateInvitationCommand", "ApplicationRequest", "Succeeded"),
            new AuditEntry("CreateInvitationCommand", "ApplicationRequest", "Failed", error: "InvalidOperationException"),
            new AuditEntry("AcceptInvitationCommand", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();

        var byAction = await handler.Handle(new SearchAuditEntriesQuery(Action: "CreateInvitationCommand"), CancellationToken.None);
        byAction.TotalCount.Should().Be(2);

        var failed = await handler.Handle(new SearchAuditEntriesQuery(Outcome: "Failed"), CancellationToken.None);
        failed.TotalCount.Should().Be(1);
        failed.Items.Should().ContainSingle().Which.Action.Should().Be("CreateInvitationCommand");
    }

    [Fact]
    public async Task Search_ShouldPaginateNewestFirst()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        for (var index = 0; index < 5; index++)
            await context.AuditEntries.AddAsync(new AuditEntry($"Action{index}", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();

        var firstPage = await handler.Handle(new SearchAuditEntriesQuery(Page: 1, PageSize: 2), CancellationToken.None);
        firstPage.TotalCount.Should().Be(5);
        firstPage.Items.Should().HaveCount(2);
        firstPage.Items.First().Action.Should().Be("Action4");

        var thirdPage = await handler.Handle(new SearchAuditEntriesQuery(Page: 3, PageSize: 2), CancellationToken.None);
        thirdPage.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_ShouldClampPaging()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        await context.AuditEntries.AddAsync(new AuditEntry("Action", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();
        var result = await handler.Handle(new SearchAuditEntriesQuery(Page: 0, PageSize: 500), CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_ShouldResolveActorNameByEmailIgnoringCase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        context.DomainUsers.Add(User.Create("admin@example.com", "Admin da Silva"));
        await context.SaveChangesAsync();

        // Caixa mista no gravado (login sem normalizar), e-mail de usuário
        // removido e linha de sistema — os três casos de exibição do front.
        await context.AuditEntries.AddRangeAsync(
            new AuditEntry("CreateTaskCommand", "ApplicationRequest", "Succeeded", actorId: "Admin@Example.com"),
            new AuditEntry("DeleteTaskCommand", "ApplicationRequest", "Succeeded", actorId: "removido@example.com"),
            new AuditEntry("SystemCommand", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();

        var result = await handler.Handle(new SearchAuditEntriesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Should().ContainSingle(entry => entry.ActorId == "Admin@Example.com")
            .Which.ActorName.Should().Be("Admin da Silva");
        result.Items.Should().ContainSingle(entry => entry.ActorId == "removido@example.com")
            .Which.ActorName.Should().BeNull();
        result.Items.Should().ContainSingle(entry => entry.ActorId == null)
            .Which.ActorName.Should().BeNull();
    }

    /// <summary>
    /// F50-T004 — filtro por ator: casa pelo USERNAME (e-mail gravado em
    /// `ActorId`), pelo SOBRENOME/nome completo (`DisplayName`/`FullName`
    /// do usuário = o nome do operador exibido na auditoria) e por
    /// fragmento, tudo case-insensitive. Linha de sistema (`ActorId` null)
    /// jamais entra no filtro.
    /// </summary>
    [Fact]
    public async Task Search_ShouldFilterByActorMatchingUsernameSurnameOrOperatorName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        context.DomainUsers.AddRange(
            User.Create("ana@example.com", "Ana da Silva", "Ana Maria da Silva"),
            User.Create("bruno@example.com", "Bruno Costa"));
        await context.SaveChangesAsync();

        await context.AuditEntries.AddRangeAsync(
            new AuditEntry("CreateTaskCommand", "Project", "Succeeded", actorId: "ana@example.com"),
            new AuditEntry("DeleteTaskCommand", "Project", "Succeeded", actorId: "Ana@Example.com"),
            new AuditEntry("UpdateTaskCommand", "Project", "Succeeded", actorId: "bruno@example.com"),
            new AuditEntry("LoginWithMfa", "Session", "Succeeded", actorId: "removido@example.com"),
            new AuditEntry("SystemCommand", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();

        // Username completo (e a caixa mista gravada) → as duas entradas da Ana.
        var byUsername = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "ana@example.com"), CancellationToken.None);
        byUsername.TotalCount.Should().Be(2);

        // Fragmento do username.
        var byFragment = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "bruno@"), CancellationToken.None);
        byFragment.TotalCount.Should().Be(1);
        byFragment.Items.Single().ActorId.Should().Be("bruno@example.com");

        // Sobrenome (DisplayName/FullName) — como se fosse username.
        var bySurname = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "silva"), CancellationToken.None);
        bySurname.TotalCount.Should().Be(2);

        // Nome do operador completo, caixa alta.
        var byOperatorName = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "ANA MARIA"), CancellationToken.None);
        byOperatorName.TotalCount.Should().Be(2);

        // Usuário removido: casa só pelo e-mail gravado (sem usuário no join).
        var byRemovedEmail = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "removido"), CancellationToken.None);
        byRemovedEmail.TotalCount.Should().Be(1);

        // Sem correspondência → vazio (e a linha de sistema nunca casa).
        var noMatch = await handler.Handle(
            new SearchAuditEntriesQuery(Actor: "inexistente"), CancellationToken.None);
        noMatch.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// F50-T004 — descrição detalhada por evento: quem executou o quê, em
    /// qual recurso e com qual resultado (o erro entra na falha). Montada no
    /// handler a partir do que já existe — sem coluna nova e sem payload.
    /// </summary>
    [Fact]
    public async Task Search_ShouldComposeDetailedDescriptionPerEntry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        context.DomainUsers.Add(User.Create("ana@example.com", "Ana da Silva"));
        await context.SaveChangesAsync();

        await context.AuditEntries.AddRangeAsync(
            new AuditEntry("CreateTaskCommand", "Project", "Succeeded", actorId: "ana@example.com"),
            new AuditEntry("DeleteTaskCommand", "Project", "Failed",
                actorId: "ana@example.com", error: "InvalidOperationException"),
            new AuditEntry("SystemCommand", "ApplicationRequest", "Succeeded"));
        await context.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>>();
        var result = await handler.Handle(new SearchAuditEntriesQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle(entry => entry.Action == "CreateTaskCommand")
            .Which.Description.Should()
            .Be("Ana da Silva executou Create Task Command em Project — sucesso");
        result.Items.Should().ContainSingle(entry => entry.Action == "DeleteTaskCommand")
            .Which.Description.Should()
            .Be("Ana da Silva executou Delete Task Command em Project — falha: InvalidOperationException");
        result.Items.Should().ContainSingle(entry => entry.Action == "SystemCommand")
            .Which.Description.Should()
            .Be("Sistema executou System Command em ApplicationRequest — sucesso");
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(SearchAuditEntriesQuery).Assembly));
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IDomainEventService, DomainEventService>();
        services.AddScoped<IAuditReader, AuditReader>();
        return services.BuildServiceProvider();
    }
}
