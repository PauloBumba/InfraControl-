using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

// F50-T007/ADR-067 — eventos CRUD do checklist genérico (ADR-030 §5),
// espelhando os do TaskCriterion com o dono polimórfico junto.

public sealed class ChecklistItemCreatedDomainEvent : DomainEvent
{
    public ChecklistItemCreatedDomainEvent(
        Guid checklistItemId, ChecklistOwnerType ownerType, Guid ownerId)
    {
        ChecklistItemId = checklistItemId;
        OwnerType = ownerType;
        OwnerId = ownerId;
    }

    public Guid ChecklistItemId { get; }
    public ChecklistOwnerType OwnerType { get; }
    public Guid OwnerId { get; }
    public override string EventType => nameof(ChecklistItemCreatedDomainEvent);
}

public sealed class ChecklistItemUpdatedDomainEvent : DomainEvent
{
    public ChecklistItemUpdatedDomainEvent(
        Guid checklistItemId, ChecklistOwnerType ownerType, Guid ownerId)
    {
        ChecklistItemId = checklistItemId;
        OwnerType = ownerType;
        OwnerId = ownerId;
    }

    public Guid ChecklistItemId { get; }
    public ChecklistOwnerType OwnerType { get; }
    public Guid OwnerId { get; }
    public override string EventType => nameof(ChecklistItemUpdatedDomainEvent);
}

public sealed class ChecklistItemDeletedDomainEvent : DomainEvent
{
    public ChecklistItemDeletedDomainEvent(
        Guid checklistItemId, ChecklistOwnerType ownerType, Guid ownerId)
    {
        ChecklistItemId = checklistItemId;
        OwnerType = ownerType;
        OwnerId = ownerId;
    }

    public Guid ChecklistItemId { get; }
    public ChecklistOwnerType OwnerType { get; }
    public Guid OwnerId { get; }
    public override string EventType => nameof(ChecklistItemDeletedDomainEvent);
}
