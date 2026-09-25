using InfraControl.Application.Common.Interfaces;
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

        var outboxDeleted = await DeleteOutboxAsync(_options.OutboxCutoff(utcNow), batchSize, cancellationToken);
        var processedDeleted = await DeleteProcessedAsync(_options.ProcessedCutoff(utcNow), batchSize, cancellationToken);
        var auditDeleted = await DeleteAuditAsync(_options.AuditCutoff(utcNow), batchSize, cancellationToken);
        var monitoringDeleted = await DeleteMonitoringAsync(_options.MonitoringCutoff(utcNow), batchSize, cancellationToken);
        var notificationDeleted = await DeleteNotificationsAsync(_options.NotificationsCutoff(utcNow), batchSize, cancellationToken);
        var remoteSessionDeleted = await DeleteRemoteSessionsAsync(_options.RemoteSessionsCutoff(utcNow), batchSize, cancellationToken);
        var discoveryDeleted = await DeleteDiscoveryRunsAsync(_options.DiscoveryCutoff(utcNow), batchSize, cancellationToken);

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
}
