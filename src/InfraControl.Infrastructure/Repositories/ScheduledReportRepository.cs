using InfraControl.Application.Reports.Scheduled.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class ScheduledReportRepository(AppDbContext dbContext)
    : IScheduledReportRepository
{
    public Task<ScheduledReport?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.ScheduledReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(
        ScheduledReport entity, CancellationToken cancellationToken = default)
        => await dbContext.ScheduledReports.AddAsync(entity, cancellationToken);

    public void Remove(ScheduledReport entity)
        => dbContext.ScheduledReports.Remove(entity);

    public Task<ScheduledReport?> GetByKindAsync(
        string kind, CancellationToken cancellationToken = default)
        => dbContext.ScheduledReports
            .FirstOrDefaultAsync(r => r.Kind == kind, cancellationToken);

    public async Task<IReadOnlyList<ScheduledReport>> ListDueAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default)
        => await dbContext.ScheduledReports
            .Where(r => r.IsActive && r.NextRunAtUtc != null && r.NextRunAtUtc <= nowUtc)
            .OrderBy(r => r.NextRunAtUtc)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
