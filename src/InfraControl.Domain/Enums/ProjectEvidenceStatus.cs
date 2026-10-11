namespace InfraControl.Domain.Enums;

/// <summary>Status da evidência do Projeto (ADR-043 · docs/38 §3.4).</summary>
public enum ProjectEvidenceStatus
{
    Pending = 1,
    Validated = 2,
    Rejected = 3
}

/// <summary>Decisão de revisão da evidência.</summary>
public enum ProjectEvidenceReviewDecision
{
    Validated = 1,
    Rejected = 2
}
