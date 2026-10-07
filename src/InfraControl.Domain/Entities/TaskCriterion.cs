using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Critério de aceite de uma Task (F23-T003 — Plano de Execução §12):
/// item verificável que a task precisa cumprir para ser considerada
/// concluída. Mesmo molde dos comentários (FK TaskId cascade, CRUD
/// completo, verificação marcável/desmarcável com carimbo `DoneAt`).
/// </summary>
public sealed class TaskCriterion : AggregateRoot
{
    public const int DescriptionMaxLength = 300;

    public Guid TaskId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public bool IsDone { get; private set; }
    public DateTime? DoneAt { get; private set; }

    private TaskCriterion() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskCriterionDeletedDomainEvent(Id, TaskId));

    public static TaskCriterion Create(Guid taskId, string description)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException(
                "O id da task é obrigatório.", nameof(taskId));

        var criterion = new TaskCriterion
        {
            TaskId = taskId,
            Description = ValidateDescription(description)
        };
        criterion.RaiseDomainEvent(new TaskCriterionCreatedDomainEvent(
            criterion.Id, criterion.TaskId));
        return criterion;
    }

    /// <summary>ADR-030 §1 — descrição editável (mesmos guards do Create).</summary>
    public void EditDescription(string description)
    {
        Description = ValidateDescription(description);
        RaiseDomainEvent(new TaskCriterionUpdatedDomainEvent(Id, TaskId));
    }

    /// <summary>
    /// Marca/desmarca como atendido — `DoneAt` acompanha a marcação
    /// (null ao desmarcar). O handler decide o histórico (CriterionUpdated).
    /// </summary>
    public void SetDone(bool done)
    {
        IsDone = done;
        DoneAt = done ? DateTime.UtcNow : null;
        RaiseDomainEvent(new TaskCriterionUpdatedDomainEvent(Id, TaskId));
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A descrição do critério é obrigatória.", nameof(description));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"O critério pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        return description;
    }
}
