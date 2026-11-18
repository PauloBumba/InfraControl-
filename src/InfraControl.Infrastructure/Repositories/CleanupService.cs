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
        var monitoringEventsDeleted = await DeleteMonitoringEventsAsync(Cutoff(days, RetentionDataType.MonitoringEvents, _options.MonitoringEventsDays, utcNow), batchSize, cancellationToken);
        var unifiSyncRunsDeleted = await DeleteUniFiSyncRunsAsync(Cutoff(days, RetentionDataType.UniFiSyncRuns, _options.UniFiSyncRunsDays, utcNow), batchSize, cancellationToken);
        var invitationsDeleted = await DeleteInvitationsAsync(Cutoff(days, RetentionDataType.Invitations, _options.InvitationsDays, utcNow), batchSize, cancellationToken);
        var occurrenceTransitionsDeleted = await DeleteOccurrenceTransitionsAsync(Cutoff(days, RetentionDataType.OccurrenceTransitions, _options.OccurrenceTransitionsDays, utcNow), batchSize, cancellationToken);
        var tableRowsDeleted = await DeleteTableRetentionAsync(utcNow, batchSize, cancellationToken);

        return new CleanupResult(
            outboxDeleted,
            processedDeleted,
            auditDeleted,
            monitoringDeleted,
            notificationDeleted,
            remoteSessionDeleted,
            discoveryDeleted,
            monitoringEventsDeleted,
            invitationsDeleted,
            UniFiSyncRunsDeleted: unifiSyncRunsDeleted,
            OccurrenceTransitionsDeleted: occurrenceTransitionsDeleted,
            TableRowsDeleted: tableRowsDeleted);
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

    private async Task<int> DeleteMonitoringEventsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.MonitoringEvents
            .Where(m => m.OccurredAt < cutoff)
            .OrderBy(m => m.OccurredAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.MonitoringEvents
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteInvitationsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Invitations
            .Where(m => m.CreateDateTime < cutoff)
            .OrderBy(m => m.CreateDateTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.Invitations
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteUniFiSyncRunsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.UniFiSyncRuns
            .Where(m => m.StartedAtUtc < cutoff)
            .OrderBy(m => m.StartedAtUtc)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.UniFiSyncRuns
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Poda o histórico append-only de transições de ocorrência (F55) —
    /// a <see cref="Domain.Entities.Occurrence"/> atual é estado de dedup e
    /// NÃO é tocada; só as transições antigas saem.
    /// </summary>
    private async Task<int> DeleteOccurrenceTransitionsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.OccurrenceTransitions
            .Where(m => m.CreateDateTime < cutoff)
            .OrderBy(m => m.CreateDateTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.OccurrenceTransitions
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Retenção genérica por tabela (metodologia 2026-11-15): para cada
    /// <see cref="Domain.Entities.TableRetentionPolicy"/>, resolve a entidade
    /// no EF model e apaga em lote as linhas com <c>CreateDateTime</c> &lt;
    /// cutoff. Entidade que sumiu do model é pulada (política fica órfã,
    /// visível na tela — nunca inventa delete).
    /// </summary>
    private async Task<int> DeleteTableRetentionAsync(
        DateTime utcNow, int batchSize, CancellationToken cancellationToken)
    {
        var policies = await _dbContext.TableRetentionPolicies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var deleted = 0;
        foreach (var policy in policies)
        {
            var entityType = _dbContext.Model.GetEntityTypes()
                .FirstOrDefault(e => e.ClrType.Name == policy.EntityName
                    && e.GetTableName() is not null);
            if (entityType is null)
                continue;

            var cutoff = utcNow.AddDays(-policy.Days);
            deleted += await (Task<int>)DeleteRowsMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [cutoff, batchSize, cancellationToken])!;
        }

        return deleted;
    }

    /// <summary>
    /// Poda genérica por <c>CreateDateTime</c>: invocada por reflection
    /// porque o tipo da entidade só é conhecido em runtime (tabela nova
    /// não exige código novo — só a política do admin).
    /// </summary>
    private Task<int> DeleteRowsAsync<TEntity>(
        DateTime cutoff, int batchSize, CancellationToken cancellationToken)
        where TEntity : class
        => _dbContext.Set<TEntity>()
            .Where(e => EF.Property<DateTime>(e, "CreateDateTime") < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    private static readonly System.Reflection.MethodInfo DeleteRowsMethod =
        typeof(CleanupService).GetMethod(
            nameof(DeleteRowsAsync),
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)!;

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
