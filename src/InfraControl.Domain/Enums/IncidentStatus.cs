namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados do Incident (F6-T003 — 5 estados da UI); transições livres
/// dentro do enum (≠ atual); Resolved estampa ResolvedAt (reopen limpa).
/// </summary>
public enum IncidentStatus
{
    Open = 0,
    Investigating = 1,
    Identified = 2,
    Monitoring = 3,
    Resolved = 4
}
