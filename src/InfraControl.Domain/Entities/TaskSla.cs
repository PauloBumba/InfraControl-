using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// SLA aplicado a uma Task (F8-T003) — snapshot dos prazos da política
/// no momento da aplicação: mudanças futuras na `SlaPolicy` não alteram
/// os prazos históricos (docs/04). PK = TaskId (1:1 por task);
/// `TaskSlaPause` (F8-T004) somará pausas aos vencimentos.
/// </summary>
public sealed class TaskSla : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid SlaPolicyId { get; private set; }

    // Snapshot da política (não referenciado — histórico preservado).
    public int ResponseMinutes { get; private set; }
    public int ResolutionMinutes { get; private set; }

    public DateTime ResponseDueAt { get; private set; }
    public DateTime ResolutionDueAt { get; private set; }

    private TaskSla() { }

    /// <summary>Aplica a política a partir da abertura da task (CreateDateTime).</summary>
    public static TaskSla Apply(TaskItem task, SlaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(policy);

        var sla = new TaskSla
        {
            TaskId = task.Id,
            SlaPolicyId = policy.Id,
            ResponseMinutes = policy.ResponseMinutes,
            ResolutionMinutes = policy.ResolutionMinutes,
            ResponseDueAt = task.CreateDateTime.AddMinutes(policy.ResponseMinutes),
            ResolutionDueAt = task.CreateDateTime.AddMinutes(policy.ResolutionMinutes)
        };
        sla.RaiseDomainEvent(new TaskSlaCreatedDomainEvent(sla.TaskId, sla.SlaPolicyId));
        return sla;
    }

    /// <summary>
    /// F8-T004: soma uma pausa fechada aos vencimentos — o snapshot de
    /// minutos permanece (docs/04: prazos históricos preservados).
    /// </summary>
    public void ExtendDueDates(TimeSpan paused)
    {
        if (paused <= TimeSpan.Zero)
            return;

        ResponseDueAt = ResponseDueAt.Add(paused);
        ResolutionDueAt = ResolutionDueAt.Add(paused);
        RaiseDomainEvent(new TaskSlaUpdatedDomainEvent(
            TaskId, ResponseDueAt, ResolutionDueAt));
    }
}
