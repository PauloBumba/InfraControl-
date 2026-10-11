using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório de evidências do projeto.
/// </summary>
public sealed class ProjectEvidenceRepository(AppDbContext dbContext) : IProjectEvidenceRepository
{
    public Task<ProjectEvidence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectEvidences
            .FirstOrDefaultAsync(evidence => evidence.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectEvidence>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default)
        => await dbContext.ProjectEvidences
            .Where(evidence => evidence.ProjectId == projectId)
            .OrderByDescending(evidence => evidence.CreateDateTime)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProjectEvidence entity, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectEvidences.AddAsync(entity, cancellationToken);

    public void Remove(ProjectEvidence entity) =>
        dbContext.ProjectEvidences.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
