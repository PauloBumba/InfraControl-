namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Acompanhamento do PDI (docs/30 §9-§11, §13, Fase 3 — ADR-033) —
/// datas em "yyyy-MM-dd" (DateOnly); <c>Status</c> do impedimento aceita
/// case-insensitive (Open, Resolved) e o domínio responde 400 pt-BR.
/// No check-in, o PUT é por lado: relato (dono) e resposta (gestor) —
/// campos não enviados permanecem intactos.
/// </summary>
public sealed record CreatePdiProgressViewModel(
    Guid PhaseId,
    int? Percent = null,
    string Comment = "");

public sealed record CreatePdiCheckInViewModel(
    Guid PhaseId,
    DateOnly CheckInDate,
    string WhatDone,
    string? WhatLearned = null,
    string? Difficulty = null,
    string? NeedSupport = null);

public sealed record UpdatePdiCheckInViewModel(
    string? WhatDone = null,
    string? WhatLearned = null,
    string? Difficulty = null,
    string? NeedSupport = null,
    string? ManagerFeedback = null,
    string? NextAction = null);

public sealed record CreatePdiImpedimentViewModel(
    Guid PhaseId,
    string Type,
    string Description,
    DateOnly OccurredAt,
    string? Impact = null,
    Guid? ResponsibleId = null,
    string? ActionTaken = null);

public sealed record UpdatePdiImpedimentViewModel(
    string Type,
    string Description,
    DateOnly OccurredAt,
    string? Impact = null,
    Guid? ResponsibleId = null,
    string? ActionTaken = null,
    string Status = "Open");

public sealed record CreatePdiFeedbackViewModel(
    Guid PhaseId,
    Guid RecipientId,
    string Type,
    string Content,
    DateOnly FeedbackDate);
