namespace InfraControl.Domain.Enums;

/// <summary>
/// Decisão do gestor na validação de uma fase (§15 — Fase 5, ADR-035):
/// valida (fase concluída) ou rejeita (fase volta a InProgress com
/// motivo obrigatório).
/// </summary>
public enum PdiPhaseValidationDecision
{
    Validated = 1,
    Rejected = 2
}
