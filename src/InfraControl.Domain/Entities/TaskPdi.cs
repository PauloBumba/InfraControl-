using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Vínculo N:N Task ↔ PDI (F23-T005/P5b — spec Tasks §15; alvo decidido
/// pelo usuário: "Task ↔ PDI (plano do usuário)"). A chave do plano no
/// produto é o dono (1 plano vigente por usuário — a própria API usa
/// `/pdi/{userId}`), então a junção carrega `user_id`; refs de usuário
/// sem FK (critério do ADR-029 — `UserId`/`ManagerId` do plano próprio
/// também não têm FK).
/// </summary>
public sealed class TaskPdi : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }

    private TaskPdi() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskPdiDeletedDomainEvent(TaskId, UserId));

    public static TaskPdi Create(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "O usuário dono do PDI é obrigatório.", nameof(userId));

        var link = new TaskPdi { TaskId = taskId, UserId = userId };
        link.RaiseDomainEvent(new TaskPdiCreatedDomainEvent(link.TaskId, link.UserId));
        return link;
    }
}
