using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório do histórico append-only do projeto.
/// </summary>
public sealed class ProjectHistoryRepository(AppDbContext dbContext) : IProjectHistoryRepository
{
    public Task<ProjectHistoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectHistoryEntries
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectHistoryEntry>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default)
        => await dbContext.ProjectHistoryEntries
            .Where(entry => entry.ProjectId == projectId)
            .OrderByDescending(entry => entry.CreateDateTime)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProjectHistoryEntry entity, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectHistoryEntries.AddAsync(entity, cancellationToken);

    public void Remove(ProjectHistoryEntry entity) =>
        dbContext.ProjectHistoryEntries.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
