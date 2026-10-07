using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Impedimento estruturado de uma Task (F23-T004/P4b — Plano de Execução
/// §10): registro auditável do bloqueio com resolução carimbada. Múltiplos
/// por task; o estado do workflow (Blocked) continua INDEPENDENTE — este
/// registro responde "por que está travado" sem mudar o ciclo de estados
/// (mudança de workflow exigiria ADR). Molde dos critérios (FK TaskId
/// cascade, CRUD completo, eventos ADR-030).
/// </summary>
public sealed class TaskImpediment : AggregateRoot
{
    public const int DescriptionMaxLength = 300;

    public Guid TaskId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    /// <summary>Null = em aberto; quem detectou/resolveu vive no histórico.</summary>
    public DateTime? ResolvedAt { get; private set; }

    private TaskImpediment() { }

    public bool IsResolved => ResolvedAt != null;

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskImpedimentDeletedDomainEvent(Id, TaskId));

    public static TaskImpediment Create(Guid taskId, string description)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException(
                "O id da task é obrigatório.", nameof(taskId));

        var impediment = new TaskImpediment
        {
            TaskId = taskId,
            Description = ValidateDescription(description)
        };
        impediment.RaiseDomainEvent(new TaskImpedimentCreatedDomainEvent(
            impediment.Id, impediment.TaskId));
        return impediment;
    }

    /// <summary>ADR-030 §1 — descrição editável (mesmos guards do Create).</summary>
    public void EditDescription(string description)
    {
        Description = ValidateDescription(description);
        RaiseDomainEvent(new TaskImpedimentUpdatedDomainEvent(Id, TaskId));
    }

    /// <summary>
    /// Resolve (carimba `ResolvedAt`) ou reabre (limpa) — o handler decide
    /// o histórico (ImpedimentResolved/ImpedimentReopened) e o no-op.
    /// </summary>
    public void SetResolved(bool resolved)
    {
        ResolvedAt = resolved ? DateTime.UtcNow : null;
        RaiseDomainEvent(new TaskImpedimentUpdatedDomainEvent(Id, TaskId));
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A descrição do impedimento é obrigatória.", nameof(description));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"O impedimento pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        return description;
    }
}
