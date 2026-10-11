using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Repositório do agregado Project (ADR-043 · docs/38).
/// </summary>
public sealed class ProjectRepository(AppDbContext dbContext) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Project entity, CancellationToken cancellationToken = default) =>
        await dbContext.Projects.AddAsync(entity, cancellationToken);

    public void Remove(Project entity) =>
        dbContext.Projects.Remove(entity);

    public Task<Project?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task<string> GetNextCodeAsync(CancellationToken cancellationToken = default)
    {
        var count = await dbContext.Projects.CountAsync(cancellationToken);
        var nextNumber = count + 1;
        var candidate = $"PRJ-{nextNumber:D3}";

        while (await dbContext.Projects.AnyAsync(p => p.Code == candidate, cancellationToken))
        {
            nextNumber++;
            candidate = $"PRJ-{nextNumber:D3}";
        }

        return candidate;
    }

    public async Task<PagedResult<Project>> SearchAsync(
        string? term,
        ProjectStatus? status,
        ProjectPriority? priority,
        Guid? leaderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Projects.AsNoTracking()
            .Include(p => p.Members)
            .Where(p =>
                (status == null || p.Status == status)
                && (priority == null || p.Priority == priority)
                && (leaderId == null || p.LeaderId == leaderId)
                && (term == null
                    || p.Code.Contains(term)
                    || p.Title.Contains(term)
                    || p.Description.Contains(term)))
            .OrderByDescending(p => p.CreateDateTime);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Project>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyCollection<Project>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Members)
            .OrderByDescending(p => p.CreateDateTime)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
