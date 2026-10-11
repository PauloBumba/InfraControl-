using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório do acompanhamento de progresso do projeto.
/// </summary>
public sealed class ProjectProgressRepository(AppDbContext dbContext) : IProjectProgressRepository
{
    public Task<ProjectProgress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProjectProgresses
            .FirstOrDefaultAsync(progress => progress.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectProgress>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default)
        => await dbContext.ProjectProgresses
            .Where(progress => progress.ProjectId == projectId)
            .OrderByDescending(progress => progress.CreateDateTime)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProjectProgress entity, CancellationToken cancellationToken = default) =>
        await dbContext.ProjectProgresses.AddAsync(entity, cancellationToken);

    public void Remove(ProjectProgress entity) =>
        dbContext.ProjectProgresses.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
