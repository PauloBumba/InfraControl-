using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
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
        options.MonitoringResultsDays.Should().Be(180);
        options.NotificationsDays.Should().Be(90);
        options.RemoteSessionsDays.Should().Be(90);
        options.DiscoveryRunsDays.Should().Be(180);
        options.MonitoringEventsDays.Should().Be(90);
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
        options.MonitoringCutoff(utcNow).Should().Be(utcNow.AddDays(-180));
        options.NotificationsCutoff(utcNow).Should().Be(utcNow.AddDays(-90));
        options.RemoteSessionsCutoff(utcNow).Should().Be(utcNow.AddDays(-90));
        options.DiscoveryCutoff(utcNow).Should().Be(utcNow.AddDays(-180));
        options.MonitoringEventsCutoff(utcNow).Should().Be(utcNow.AddDays(-90));
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

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredHistoryAndKeepRecentActiveAndInventory()
    {
        var utcNow = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        var deviceId = await SeedDeviceAsync(context);

        var oldResult = MonitoringResult.Create(deviceId, CheckKind.Ping, true, 12);
        var recentResult = MonitoringResult.Create(deviceId, CheckKind.Ping, true, 9);
        var oldNotification = Notification.Create(Guid.NewGuid(), "IncidentOpened", "titulo", "mensagem");
        var recentNotification = Notification.Create(Guid.NewGuid(), "IncidentOpened", "titulo", "mensagem");
        var oldSession = RemoteSession.Open(deviceId, Guid.NewGuid(), RemoteProtocol.Ssh);
        oldSession.Close();
        var recentSession = RemoteSession.Open(deviceId, Guid.NewGuid(), RemoteProtocol.Ssh);
        recentSession.Close();
        var oldRun = DiscoveryRun.Start(deviceId);
        oldRun.Complete();
        var recentRun = DiscoveryRun.Start(deviceId);
        recentRun.Complete();
        var activeRun = DiscoveryRun.Start(deviceId);

        await context.AddRangeAsync(
            oldResult, recentResult, oldNotification, recentNotification,
            oldSession, recentSession, oldRun, recentRun, activeRun);
        await context.SaveChangesAsync();

        context.Entry(oldResult).Property(nameof(Entity.CreateDateTime))
            .CurrentValue = utcNow.AddDays(-200);
        context.Entry(recentResult).Property(nameof(Entity.CreateDateTime))
            .CurrentValue = utcNow.AddDays(-1);
        context.Entry(oldNotification).Property(nameof(Entity.CreateDateTime))
            .CurrentValue = utcNow.AddDays(-100);
        context.Entry(recentNotification).Property(nameof(Entity.CreateDateTime))
            .CurrentValue = utcNow.AddDays(-1);
        context.Entry(oldSession).Property(nameof(RemoteSession.StartedAt))
            .CurrentValue = utcNow.AddDays(-100);
        context.Entry(recentSession).Property(nameof(RemoteSession.StartedAt))
            .CurrentValue = utcNow.AddDays(-1);
        context.Entry(oldRun).Property(nameof(DiscoveryRun.StartedAt))
            .CurrentValue = utcNow.AddDays(-200);
        context.Entry(oldRun).Property(nameof(DiscoveryRun.FinishedAt))
            .CurrentValue = utcNow.AddDays(-200);
        context.Entry(recentRun).Property(nameof(DiscoveryRun.StartedAt))
            .CurrentValue = utcNow.AddDays(-1);
        context.Entry(recentRun).Property(nameof(DiscoveryRun.FinishedAt))
            .CurrentValue = utcNow.AddDays(-1);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var result = await cleanup.CleanupAsync(utcNow);

        result.MonitoringDeleted.Should().Be(1);
        result.NotificationDeleted.Should().Be(1);
        result.RemoteSessionDeleted.Should().Be(1);
        result.DiscoveryDeleted.Should().Be(1);

        (await context.MonitoringResults.CountAsync()).Should().Be(1);
        (await context.Notifications.CountAsync()).Should().Be(1);
        (await context.RemoteSessions.CountAsync()).Should().Be(1);
        (await context.DiscoveryRuns.CountAsync()).Should().Be(2);

        // regra do docs/11: inventário atual nunca é tocado
        (await context.Devices.CountAsync()).Should().Be(1);
        (await context.Locations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CleanupAsync_ShouldUsePolicyOverConfigDefault()
    {
        var utcNow = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        // config diz 90 dias; a policy de 7 dias deve prevalecer
        context.RetentionPolicies.Add(
            RetentionPolicy.Create(RetentionDataType.AuditEntries, 7));
        var expiredUnderPolicy = new AuditEntry("Create", "User", "Succeeded");
        var recent = new AuditEntry("Create", "User", "Succeeded");
        await context.AddRangeAsync(expiredUnderPolicy, recent);
        await context.SaveChangesAsync();

        context.Entry(expiredUnderPolicy).Property(nameof(AuditEntry.OccurredAt))
            .CurrentValue = utcNow.AddDays(-30);
        context.Entry(recent).Property(nameof(AuditEntry.OccurredAt))
            .CurrentValue = utcNow.AddDays(-1);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var result = await cleanup.CleanupAsync(utcNow);

        result.AuditDeleted.Should().Be(1);
        (await context.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredMonitoringEventsAndKeepLinkedAlert()
    {
        var utcNow = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        // cutoff de 90 dias sobre OccurredAt (não CreateDateTime)
        var oldEvent = MonitoringEvent.CreateProblem(
            MonitoringSource.Zabbix, "ev-old", null, "ping", 3,
            utcNow.AddDays(-100), "problem antigo", null);
        var recentEvent = MonitoringEvent.CreateProblem(
            MonitoringSource.Zabbix, "ev-new", null, "ping", 3,
            utcNow.AddDays(-1), "problem recente", null);
        await context.AddRangeAsync(oldEvent, recentEvent);
        await context.SaveChangesAsync();

        var linkedAlert = Alert.CreateExternal(
            oldEvent.Id, "ev-old", null, null, AlertSeverity.Critical,
            "problem antigo", MonitoringSource.Zabbix);
        context.Alerts.Add(linkedAlert);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var result = await cleanup.CleanupAsync(utcNow);

        result.MonitoringEventsDeleted.Should().Be(1);
        (await context.MonitoringEvents.CountAsync()).Should().Be(1);

        // FK alert.monitoring_event_id = SetNull: o alerta sobrevive sem o evento
        var alert = await context.Alerts.SingleAsync(a => a.Id == linkedAlert.Id);
        alert.MonitoringEventId.Should().BeNull();
        alert.ExternalProblemId.Should().Be("ev-old");
    }

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, $"dev-{Guid.NewGuid():N}"[..12], DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
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
            options.MonitoringResultsDays = 180;
            options.NotificationsDays = 90;
            options.RemoteSessionsDays = 90;
            options.DiscoveryRunsDays = 180;
            options.MonitoringEventsDays = 90;
            options.BatchSize = 500;
        });
        services.AddScoped<ICleanupService, CleanupService>();
        return services.BuildServiceProvider();
    }
}
