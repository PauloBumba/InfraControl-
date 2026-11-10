using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Checklists.Ports;

/// <summary>Itens do checklist genérico (F50-T007/ADR-067).</summary>
public interface IChecklistItemRepository : IRepository<ChecklistItem>
{
    Task<IReadOnlyCollection<ChecklistItem>> GetByOwnerAsync(
        ChecklistOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
