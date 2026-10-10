using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório de impedimentos do projeto.
/// </summary>
public sealed class ProjectImpedimentRepository(AppDbContext dbContext) : IProjectImpedimentRepository
{
    public Task<ProjectImpediment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectImpediments
            .FirstOrDefaultAsync(impediment => impediment.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectImpediment>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default)
        => await dbContext.ProjectImpediments
            .Where(impediment => impediment.ProjectId == projectId)
            .OrderByDescending(impediment => impediment.CreateDateTime)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProjectImpediment entity, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectImpediments.AddAsync(entity, cancellationToken);

    public void Remove(ProjectImpediment entity) =>
        dbContext.ProjectImpediments.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
