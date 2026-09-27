using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Usuário responsável (F7-T002).</summary>
public sealed class TaskAssignee : Entity
{
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }

    private TaskAssignee() { }

    public static TaskAssignee Create(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        return new TaskAssignee { TaskId = taskId, UserId = userId };
    }
}
