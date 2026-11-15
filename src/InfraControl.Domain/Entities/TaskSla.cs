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

    /// <summary>
    /// Instante da violação de cada prazo (null = dentro do SLA ou ainda
    /// não verificado). O SlaBreachWorker marca uma única vez — evento
    /// duplicado de breach é impossível por construção.
    /// </summary>
    public DateTime? ResponseBreachedAt { get; private set; }
    public DateTime? ResolutionBreachedAt { get; private set; }

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

    /// <summary>
    /// Marca a violação de um prazo (worker de SLA). Idempotente: prazo já
    /// marcado não emite evento de novo. Retorna true se marcou agora.
    /// </summary>
    public bool MarkBreached(SlaBreachKind kind, DateTime breachedAtUtc, string taskTitle)
    {
        var dueAt = kind == SlaBreachKind.Response ? ResponseDueAt : ResolutionDueAt;
        var already = kind == SlaBreachKind.Response
            ? ResponseBreachedAt : ResolutionBreachedAt;
        if (already is not null || dueAt > breachedAtUtc)
            return false;

        if (kind == SlaBreachKind.Response)
            ResponseBreachedAt = breachedAtUtc;
        else
            ResolutionBreachedAt = breachedAtUtc;

        RaiseDomainEvent(new TaskSlaBreachedDomainEvent(
            TaskId, SlaPolicyId, taskTitle,
            kind == SlaBreachKind.Response ? "resposta" : "resolução",
            dueAt));
        return true;
    }
}

/// <summary>Qual prazo do SLA estourou.</summary>
public enum SlaBreachKind
{
    Response,
    Resolution
}
