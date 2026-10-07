using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Usuário responsável (F7-T002).</summary>
public sealed class TaskAssignee : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }

    private TaskAssignee() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskAssigneeDeletedDomainEvent(TaskId, UserId));

    public static TaskAssignee Create(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        var link = new TaskAssignee { TaskId = taskId, UserId = userId };
        link.RaiseDomainEvent(new TaskAssigneeCreatedDomainEvent(link.TaskId, link.UserId));
        return link;
    }
}
