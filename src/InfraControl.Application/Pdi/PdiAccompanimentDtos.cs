namespace InfraControl.Application.Pdi;

/// <summary>
/// Acompanhamento da fase (docs/30 §9-§11, §13, Fase 3 — ADR-033):
/// progresso/comentários, check-ins, impedimentos e feedbacks do plano,
/// agrupados no GET único do diálogo de acompanhamento.
/// </summary>
public sealed record PdiProgressDto(
    Guid Id,
    Guid PhaseId,
    Guid AuthorId,
    string? AuthorName,
    int? Percent,
    string Comment,
    DateTime CreatedAt);

public sealed record PdiCheckInDto(
    Guid Id,
    Guid PhaseId,
    DateOnly CheckInDate,
    Guid CreatedBy,
    string? CreatedByName,
    string WhatDone,
    string? WhatLearned,
    string? Difficulty,
    string? NeedSupport,
    string? ManagerFeedback,
    string? NextAction,
    DateTime? RespondedAt,
    string? RespondedByName);

public sealed record PdiImpedimentDto(
    Guid Id,
    Guid PhaseId,
    string Type,
    string Description,
    string? Impact,
    DateOnly OccurredAt,
    Guid? ResponsibleId,
    string? ResponsibleName,
    string? ActionTaken,
    string Status,
    DateOnly? ResolvedAt);

public sealed record PdiFeedbackDto(
    Guid Id,
    Guid PhaseId,
    Guid AuthorId,
    string? AuthorName,
    Guid RecipientId,
    string? RecipientName,
    string Type,
    string Content,
    DateOnly FeedbackDate);

public sealed record PdiEvidenceDto(
    Guid Id,
    Guid PhaseId,
    string Type,
    string Status,
    string? Description,
    string FileName,
    string ContentType,
    long FileSize,
    Guid CreatedBy,
    string? CreatedByName,
    DateTime CreatedAt,
    Guid? ReviewedBy,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? RejectionReason);

public sealed record PdiAccompanimentDto(
    Guid UserId,
    IReadOnlyList<PdiProgressDto> Progresses,
    IReadOnlyList<PdiCheckInDto> CheckIns,
    IReadOnlyList<PdiImpedimentDto> Impediments,
    IReadOnlyList<PdiFeedbackDto> Feedbacks,
    IReadOnlyList<PdiEvidenceDto> Evidences);
