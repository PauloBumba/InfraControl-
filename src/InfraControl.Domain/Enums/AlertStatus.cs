namespace InfraControl.Domain.Enums;

/// <summary>
/// Ciclo de vida do Alert (F6-T001): nasce Open e resolve automático
/// quando a condição normaliza no próximo check.
/// </summary>
public enum AlertStatus
{
    Open = 0,
    Resolved = 1
}
