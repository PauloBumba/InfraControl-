using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Incident (F7-T004).</summary>
public sealed class TaskIncident : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid IncidentId { get; private set; }

    private TaskIncident() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskIncidentDeletedDomainEvent(TaskId, IncidentId));

    public static TaskIncident Create(Guid taskId, Guid incidentId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (incidentId == Guid.Empty)
            throw new ArgumentException("O id do incidente é obrigatório.", nameof(incidentId));

        var link = new TaskIncident { TaskId = taskId, IncidentId = incidentId };
        link.RaiseDomainEvent(new TaskIncidentCreatedDomainEvent(link.TaskId, link.IncidentId));
        return link;
    }
}
