using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Item de checklist genérico (F50-T007/ADR-067) — espelha o
/// `TaskCriterion` (F23-T003: descrição ≤300, marcação com carimbo
/// `DoneAt`, eventos CRUD ADR-030) com dono polimórfico `OwnerType` +
/// `OwnerId` **sem FK** (precedente `KnowledgeArticleOrigin`/ADR-033);
/// a existência do dono é validada no handler por tipo.
/// </summary>
public sealed class ChecklistItem : AggregateRoot
{
    public const int DescriptionMaxLength = 300;

    public ChecklistOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public bool IsDone { get; private set; }
    public DateTime? DoneAt { get; private set; }

    private ChecklistItem() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new ChecklistItemDeletedDomainEvent(
            Id, OwnerType, OwnerId));

    public static ChecklistItem Create(
        ChecklistOwnerType ownerType, Guid ownerId, string description)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException(
                "O id do dono é obrigatório.", nameof(ownerId));

        var item = new ChecklistItem
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            Description = ValidateDescription(description),
        };
        item.RaiseDomainEvent(new ChecklistItemCreatedDomainEvent(
            item.Id, item.OwnerType, item.OwnerId));
        return item;
    }

    /// <summary>ADR-030 §1 — descrição editável (mesmos guards do Create).</summary>
    public void EditDescription(string description)
    {
        Description = ValidateDescription(description);
        RaiseDomainEvent(new ChecklistItemUpdatedDomainEvent(
            Id, OwnerType, OwnerId));
    }

    /// <summary>
    /// Marca/desmarca como atendido — `DoneAt` acompanha a marcação
    /// (null ao desmarcar), mesmo molde do `TaskCriterion`.
    /// </summary>
    public void SetDone(bool done)
    {
        IsDone = done;
        DoneAt = done ? DateTime.UtcNow : null;
        RaiseDomainEvent(new ChecklistItemUpdatedDomainEvent(
            Id, OwnerType, OwnerId));
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A descrição do item é obrigatória.", nameof(description));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"O item pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        return description;
    }
}
