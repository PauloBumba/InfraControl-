using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Incident ↔ Alert (F6-T003).</summary>
public sealed class IncidentAlert : Entity
{
    public Guid IncidentId { get; private set; }
    public Guid AlertId { get; private set; }

    private IncidentAlert() { }

    public static IncidentAlert Create(Guid incidentId, Guid alertId)
    {
        if (incidentId == Guid.Empty)
            throw new ArgumentException("O id do incidente é obrigatório.", nameof(incidentId));
        if (alertId == Guid.Empty)
            throw new ArgumentException("O id do alerta é obrigatório.", nameof(alertId));

        return new IncidentAlert { IncidentId = incidentId, AlertId = alertId };
    }
}
