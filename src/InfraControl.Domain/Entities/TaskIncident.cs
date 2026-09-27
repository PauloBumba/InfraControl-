using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ Incident (F7-T004).</summary>
public sealed class TaskIncident : Entity
{
    public Guid TaskId { get; private set; }
    public Guid IncidentId { get; private set; }

    private TaskIncident() { }

    public static TaskIncident Create(Guid taskId, Guid incidentId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (incidentId == Guid.Empty)
            throw new ArgumentException("O id do incidente é obrigatório.", nameof(incidentId));

        return new TaskIncident { TaskId = taskId, IncidentId = incidentId };
    }
}
