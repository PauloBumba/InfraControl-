using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Repositories;

public sealed class CleanupService(AppDbContext dbContext, IOptions<RetentionOptions> options) : ICleanupService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly RetentionOptions _options = options.Value;

    public async Task<CleanupResult> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, _options.BatchSize);
        var days = await LoadRetentionDaysAsync(cancellationToken);

        var outboxDeleted = await DeleteOutboxAsync(Cutoff(days, RetentionDataType.OutboxMessages, _options.OutboxProcessedDays, utcNow), batchSize, cancellationToken);
        var processedDeleted = await DeleteProcessedAsync(Cutoff(days, RetentionDataType.ProcessedMessages, _options.ProcessedMessagesDays, utcNow), batchSize, cancellationToken);
        var auditDeleted = await DeleteAuditAsync(Cutoff(days, RetentionDataType.AuditEntries, _options.AuditEntriesDays, utcNow), batchSize, cancellationToken);
        var monitoringDeleted = await DeleteMonitoringAsync(Cutoff(days, RetentionDataType.MonitoringResults, _options.MonitoringResultsDays, utcNow), batchSize, cancellationToken);
        var notificationDeleted = await DeleteNotificationsAsync(Cutoff(days, RetentionDataType.Notifications, _options.NotificationsDays, utcNow), batchSize, cancellationToken);
        var remoteSessionDeleted = await DeleteRemoteSessionsAsync(Cutoff(days, RetentionDataType.RemoteSessions, _options.RemoteSessionsDays, utcNow), batchSize, cancellationToken);
        var discoveryDeleted = await DeleteDiscoveryRunsAsync(Cutoff(days, RetentionDataType.DiscoveryRuns, _options.DiscoveryRunsDays, utcNow), batchSize, cancellationToken);

        return new CleanupResult(
            outboxDeleted,
            processedDeleted,
            auditDeleted,
            monitoringDeleted,
            notificationDeleted,
            remoteSessionDeleted,
            discoveryDeleted);
    }

    private async Task<int> DeleteOutboxAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .OrderBy(m => m.ProcessedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteProcessedAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < cutoff)
            .OrderBy(m => m.ProcessedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.ProcessedMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteAuditAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.AuditEntries
            .Where(m => m.OccurredAt < cutoff)
            .OrderBy(m => m.OccurredAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.AuditEntries
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteMonitoringAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.MonitoringResults
            .Where(m => m.CreateDateTime < cutoff)
            .OrderBy(m => m.CreateDateTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.MonitoringResults
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteNotificationsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Notifications
            .Where(m => m.CreateDateTime < cutoff)
            .OrderBy(m => m.CreateDateTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.Notifications
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteRemoteSessionsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.RemoteSessions
            .Where(m => m.StartedAt < cutoff)
            .OrderBy(m => m.StartedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.RemoteSessions
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteDiscoveryRunsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.DiscoveryRuns
            .Where(m => m.FinishedAt != null && m.FinishedAt < cutoff)
            .OrderBy(m => m.FinishedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.DiscoveryRuns
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<RetentionDataType, int>> LoadRetentionDaysAsync(
        CancellationToken cancellationToken)
        => await _dbContext.RetentionPolicies
            .AsNoTracking()
            .ToDictionaryAsync(p => p.DataType, p => p.Days, cancellationToken);

    private static DateTime Cutoff(
        IReadOnlyDictionary<RetentionDataType, int> days,
        RetentionDataType dataType,
        int configDays,
        DateTime utcNow)
        => utcNow.AddDays(-(days.GetValueOrDefault(dataType, configDays)));
}
