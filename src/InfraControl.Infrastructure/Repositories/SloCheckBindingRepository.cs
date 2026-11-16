using InfraControl.Application.Sla.Slos.Bindings.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class SloCheckBindingRepository(AppDbContext dbContext)
    : ISloCheckBindingRepository
{
    public Task<SloCheckBinding?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // A chave é composta — GetById não se aplica; mantido p/ a porta base.
        => Task.FromResult<SloCheckBinding?>(null);

    public Task AddAsync(SloCheckBinding entity, CancellationToken cancellationToken = default)
        => dbContext.SloCheckBindings.AddAsync(entity, cancellationToken).AsTask();

    public void Remove(SloCheckBinding entity)
        => dbContext.SloCheckBindings.Remove(entity);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCheckIdsBySloIdsAsync(
        IReadOnlyCollection<Guid> sloIds,
        CancellationToken cancellationToken = default)
    {
        if (sloIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var rows = await dbContext.SloCheckBindings
            .AsNoTracking()
            .Where(b => sloIds.Contains(b.SloId))
            .Select(b => new { b.SloId, b.MonitoringCheckId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(b => b.SloId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(b => b.MonitoringCheckId).ToArray());
    }

    public async Task<IReadOnlyList<SloCheckBinding>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.SloCheckBindings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // Tracking ligado: o sync do formulário Remove() estas instâncias —
    // AsNoTracking aqui conflitaria com os Adds pendentes do mesmo contexto.
    public async Task<IReadOnlyList<SloCheckBinding>> GetBySloIdAsync(
        Guid sloId, CancellationToken cancellationToken = default)
        => await dbContext.SloCheckBindings
            .Where(b => b.SloId == sloId)
            .ToListAsync(cancellationToken);

    public Task<bool> HasBindingsForCheckAsync(
        Guid monitoringCheckId, CancellationToken cancellationToken = default)
        => dbContext.SloCheckBindings.AnyAsync(
            b => b.MonitoringCheckId == monitoringCheckId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
