namespace InfraControl.Application.Checklists;

/// <summary>Item do checklist genérico na leitura (F50-T007/ADR-067).</summary>
public sealed record ChecklistItemDto(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string Description,
    bool IsDone,
    DateTime? DoneAt,
    DateTime CreatedAt);
