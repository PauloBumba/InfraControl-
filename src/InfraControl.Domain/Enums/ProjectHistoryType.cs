namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos da timeline append-only do Projeto (ADR-043 · docs/38 §3.4).
/// </summary>
public enum ProjectHistoryType
{
    Created = 1,
    Updated = 2,
    DeadlineChanged = 3,
    PhaseStarted = 4,
    PhaseSubmitted = 5,
    PhaseValidated = 6,
    PhaseRejected = 7,
    ProgressReported = 8,
    ImpedimentCreated = 9,
    ImpedimentResolved = 10,
    EvidenceAdded = 11,
    EvidenceReviewed = 12,
    SubmittedForApproval = 13,
    Approved = 14,
    Cancelled = 15,
    OnHold = 16,
    Resumed = 17
}
