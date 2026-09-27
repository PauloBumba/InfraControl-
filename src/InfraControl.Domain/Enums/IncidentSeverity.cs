namespace InfraControl.Domain.Enums;

/// <summary>
/// Severidade do Incident (F6-T003) — 4 níveis casam com a UI de
/// incidents; auto-abertura mapeia Alert Critical → Critical.
/// </summary>
public enum IncidentSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
