using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Slos.Budgets.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SloBudgetRepository(AppDbContext dbContext)
    : ISloBudgetRepository
{
    public async Task AddAsync(
        SloBudget budget, CancellationToken cancellationToken = default)
    {
        await dbContext.SloBudgets.AddAsync(budget, cancellationToken);
    }

    public Task<SloBudget?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.SloBudgets
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Remove(SloBudget budget)
        => dbContext.SloBudgets.Remove(budget);

    public async Task<PagedResult<SloBudget>> SearchBySloIdAsync(
        Guid sloId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SloBudgets
            .AsNoTracking()
            .Where(b => b.SloId == sloId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.PeriodStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SloBudget>(items, totalCount, page, pageSize);
    }

    // F18-T003: agregado por serviço — predicado aplicado ANTES do
    // COUNT (totalCount respeita o filtro) e mesma ordenação da busca
    // por SLO (PeriodStart desc).
    public async Task<PagedResult<SloBudget>> SearchByServiceIdAsync(
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SloBudgets
            .AsNoTracking()
            .Where(b => serviceId == null ||
                dbContext.Slos.Any(s => s.Id == b.SloId && s.ServiceId == serviceId));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.PeriodStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SloBudget>(items, totalCount, page, pageSize);
    }

    public Task<bool> HasSloBudgetsAsync(
        Guid sloId, CancellationToken cancellationToken = default)
        => dbContext.SloBudgets.AnyAsync(b => b.SloId == sloId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
