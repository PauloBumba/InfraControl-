using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class CleanupServiceTests
{
    [Fact]
    public void RetentionOptions_ShouldHaveExpectedDefaults()
    {
        var options = new RetentionOptions();

        options.OutboxProcessedDays.Should().Be(30);
        options.ProcessedMessagesDays.Should().Be(30);
        options.AuditEntriesDays.Should().Be(90);
        options.BatchSize.Should().Be(500);
    }

    [Fact]
    public void RetentionOptions_ShouldComputeCutoffs()
    {
        var utcNow = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var options = new RetentionOptions
        {
            OutboxProcessedDays = 30,
            ProcessedMessagesDays = 30,
            AuditEntriesDays = 90
        };

        options.OutboxCutoff(utcNow).Should().Be(utcNow.AddDays(-30));
        options.ProcessedCutoff(utcNow).Should().Be(utcNow.AddDays(-30));
        options.AuditCutoff(utcNow).Should().Be(utcNow.AddDays(-90));
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOnlyExpiredRecords()
    {
        var utcNow = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        await SeedAsync(context, utcNow);

        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var result = await cleanup.CleanupAsync(utcNow);

        result.OutboxDeleted.Should().Be(1);
        result.ProcessedDeleted.Should().Be(1);
        result.AuditDeleted.Should().Be(1);

        (await context.OutboxMessages.CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.CountAsync()).Should().Be(1);
        (await context.AuditEntries.CountAsync()).Should().Be(1);
    }

    private static async Task SeedAsync(AppDbContext context, DateTime utcNow)
    {
        var oldOutbox = new OutboxMessage("UserCreated", "{}", utcNow.AddDays(-40));
        oldOutbox.MarkProcessed();
        var recentOutbox = new OutboxMessage("UserCreated", "{}", utcNow.AddDays(-1));
        recentOutbox.MarkProcessed();
        var pendingOutbox = new OutboxMessage("UserCreated", "{}", utcNow.AddDays(-40));

        await context.OutboxMessages.AddRangeAsync(oldOutbox, recentOutbox, pendingOutbox);
        await context.SaveChangesAsync();

        context.Entry(oldOutbox).Property(nameof(OutboxMessage.ProcessedAt)).CurrentValue = utcNow.AddDays(-40);
        context.Entry(recentOutbox).Property(nameof(OutboxMessage.ProcessedAt)).CurrentValue = utcNow.AddDays(-1);
        await context.SaveChangesAsync();

        var oldProcessed = new ProcessedMessage(Guid.NewGuid(), "consumer-a");
        var recentProcessed = new ProcessedMessage(Guid.NewGuid(), "consumer-a");
        await context.ProcessedMessages.AddRangeAsync(oldProcessed, recentProcessed);
        await context.SaveChangesAsync();

        context.Entry(oldProcessed).Property(nameof(ProcessedMessage.ProcessedAt)).CurrentValue = utcNow.AddDays(-40);
        context.Entry(recentProcessed).Property(nameof(ProcessedMessage.ProcessedAt)).CurrentValue = utcNow.AddDays(-1);
        await context.SaveChangesAsync();

        var oldAudit = new AuditEntry("Create", "User", "Succeeded");
        var recentAudit = new AuditEntry("Create", "User", "Succeeded");
        await context.AuditEntries.AddRangeAsync(oldAudit, recentAudit);
        await context.SaveChangesAsync();

        context.Entry(oldAudit).Property(nameof(AuditEntry.OccurredAt)).CurrentValue = utcNow.AddDays(-100);
        context.Entry(recentAudit).Property(nameof(AuditEntry.OccurredAt)).CurrentValue = utcNow.AddDays(-1);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.Configure<RetentionOptions>(options =>
        {
            options.OutboxProcessedDays = 30;
            options.ProcessedMessagesDays = 30;
            options.AuditEntriesDays = 90;
            options.BatchSize = 500;
        });
        services.AddScoped<ICleanupService, CleanupService>();
        return services.BuildServiceProvider();
    }
}
