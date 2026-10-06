namespace InfraControl.Domain.Enums;

/// <summary>
/// Status de uma fase do desenvolvimento (docs/30 §7). Valores mínimos
/// definidos no ADR-032 — a semântica de governança completa (aprovação/
/// aceite) vem na Fase 5.
/// </summary>
public enum PdiPhaseStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3
}
