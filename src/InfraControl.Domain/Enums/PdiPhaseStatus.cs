namespace InfraControl.Domain.Enums;

/// <summary>
/// Status de uma fase do desenvolvimento (docs/30 §7). Valores mínimos
/// definidos no ADR-032; <see cref="PendingValidation"/> e o fluxo de
/// aprovação (concluir → validar/rejeitar) vêm na Fase 5 (ADR-035).
/// </summary>
public enum PdiPhaseStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,

    /// <summary>Concluída pelo dono, aguardando validação do gestor (§15).</summary>
    PendingValidation = 4
}
