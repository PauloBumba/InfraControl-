using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Notifications;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F48-T004: seed idempotente das regras dos eventos novos de conclusão
/// (Task/Project) — ON com canais Email,SignalR por padrão; linha já
/// existente é autoridade do operador (admin pode desligar sem o seeder
/// reescrever).
/// </summary>
public sealed class NotificationEventRuleSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationEventRuleSeederTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, NotificationEventRuleSeeder Seeder)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var repository = new NotificationEventRuleRepository(context);
        var seeder = new NotificationEventRuleSeeder(repository, new UnitOfWorkStub(context));
        return (context, seeder);
    }

    private sealed class UnitOfWorkStub(AppDbContext context) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => context.SaveChangesAsync(cancellationToken);

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task Seed_CreatesEnabledRulesForCompletionEvents()
    {
        var (context, seeder) = await CreateAsync();

        await seeder.SeedAsync();

        var rules = await context.NotificationEventRules.AsNoTracking()
            .OrderBy(r => r.EventType).ToListAsync();
        rules.Select(r => r.EventType).Should().BeEquivalentTo(new[]
        {
            nameof(TaskResolvedDomainEvent),
            nameof(ProjectStatusChangedDomainEvent),
        });
        rules.Should().OnlyContain(r => r.Enabled);
        rules.Should().OnlyContain(r => r.ChannelNames.SequenceEqual(
            new[] { "Email", "SignalR" }));
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        var (context, seeder) = await CreateAsync();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var count = await context.NotificationEventRules.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteDisabledRule()
    {
        var (context, seeder) = await CreateAsync();
        context.NotificationEventRules.Add(NotificationEventRule.Create(
            nameof(TaskResolvedDomainEvent), enabled: false, channels: string.Empty));
        await context.SaveChangesAsync();

        await seeder.SeedAsync();

        var rule = await context.NotificationEventRules.AsNoTracking()
            .SingleAsync(r => r.EventType == nameof(TaskResolvedDomainEvent));
        rule.Enabled.Should().BeFalse("a linha é autoridade do operador");
    }
}
