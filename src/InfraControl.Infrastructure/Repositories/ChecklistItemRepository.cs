using InfraControl.Application.Checklists.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F50-T007/ADR-067: itens do checklist genérico por dono.</summary>
public sealed class ChecklistItemRepository(AppDbContext dbContext)
    : IChecklistItemRepository
{
    public async Task<ChecklistItem?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.ChecklistItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task AddAsync(
        ChecklistItem entity, CancellationToken cancellationToken = default)
        => await dbContext.ChecklistItems.AddAsync(entity, cancellationToken);

    public void Remove(ChecklistItem entity)
        => dbContext.ChecklistItems.Remove(entity);

    public async Task<IReadOnlyCollection<ChecklistItem>> GetByOwnerAsync(
        ChecklistOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken = default)
        => await dbContext.ChecklistItems
            .AsNoTracking()
            .Where(item => item.OwnerType == ownerType && item.OwnerId == ownerId)
            .OrderBy(item => item.CreateDateTime)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
