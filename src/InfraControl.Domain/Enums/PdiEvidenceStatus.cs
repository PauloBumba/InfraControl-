namespace InfraControl.Domain.Enums;

/// <summary>
/// Status da evidência (docs/30 §12): o colaborador envia → pendente →
/// o gestor analisa → validada ou rejeitada (com motivo). Revisão
/// acontece uma única vez — validada/rejeitada não voltam a pendente.
/// </summary>
public enum PdiEvidenceStatus
{
    Pending = 1,
    Validated = 2,
    Rejected = 3
}

/// <summary>Decisão do gestor na revisão da evidência (§12).</summary>
public enum PdiEvidenceReviewDecision
{
    Validated = 1,
    Rejected = 2
}
