namespace InfraControl.Domain.Enums;

/// <summary>
/// Ciclo de vida do Alert: nasce Open e resolve automático quando a
/// condição normaliza no próximo check; F6-T004 adiciona Suppressed
/// (condição real, porém atribuída a um incidente de upstream —
/// supressão topológica).
/// </summary>
public enum AlertStatus
{
    Open = 0,
    Resolved = 1,
    Suppressed = 2
}
