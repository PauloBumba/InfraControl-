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
