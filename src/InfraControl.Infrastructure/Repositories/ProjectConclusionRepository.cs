using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório das solicitações de conclusão de Projeto.
/// </summary>
public sealed class ProjectConclusionRepository(AppDbContext dbContext)
    : IProjectConclusionRepository
{
    public Task<ProjectConclusion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectConclusions
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<ProjectConclusion>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectConclusions
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreateDateTime)
            .ToListAsync(cancellationToken);

    public Task<ProjectConclusion?> GetPendingByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.ProjectConclusions
            .Where(c => c.ProjectId == projectId
                && c.Status == ProjectConclusionStatus.Pending)
            .OrderByDescending(c => c.CreateDateTime)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(ProjectConclusion entity, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectConclusions.AddAsync(entity, cancellationToken);

    public void Remove(ProjectConclusion entity) =>
        dbContext.ProjectConclusions.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
