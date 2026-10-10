using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório da entidade ProjectPhase (ADR-043 · docs/38 §3.2).
/// </summary>
public sealed class ProjectPhaseRepository(AppDbContext dbContext) : IProjectPhaseRepository
{
    public Task<ProjectPhase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectPhases
            .FirstOrDefaultAsync(phase => phase.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<ProjectPhase>> GetByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ProjectPhases
            .Where(phase => phase.ProjectId == projectId)
            .OrderBy(phase => phase.Order)
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task AddAsync(ProjectPhase phase, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectPhases.AddAsync(phase, cancellationToken);

    public void Remove(ProjectPhase phase) =>
        dbContext.ProjectPhases.Remove(phase);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
