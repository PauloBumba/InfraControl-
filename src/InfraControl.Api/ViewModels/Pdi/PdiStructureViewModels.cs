using System.Text.Json.Serialization;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Estrutura do PDI (docs/30 §5-§8, Fase 2, ADR-032) — datas em
/// "yyyy-MM-dd" (DateOnly); <c>Status</c> da fase aceita case-insensitive
/// (NotStarted, InProgress, PendingValidation, Completed) e o domínio
/// responde 400 pt-BR.
/// </summary>
public sealed record PdiCompetencyViewModel(
    string Name,
    string? Description = null);

public sealed record CreatePdiObjectiveViewModel(
    Guid UserId,
    string Title,
    string? Description = null,
    Guid? CompetencyId = null,
    string? CurrentSituation = null,
    string? DesiredSituation = null,
    string? ExpectedResult = null,
    string? Indicator = null,
    DateOnly? TargetDate = null);

public sealed record UpdatePdiObjectiveViewModel(
    string Title,
    string? Description = null,
    Guid? CompetencyId = null,
    string? CurrentSituation = null,
    string? DesiredSituation = null,
    string? ExpectedResult = null,
    string? Indicator = null,
    DateOnly? TargetDate = null);

public sealed record CreatePdiStrategyViewModel(
    Guid ObjectiveId,
    string Title,
    string? Description = null);

public sealed record UpdatePdiStrategyViewModel(
    string Title,
    string? Description = null);

public sealed record CreatePdiPhaseViewModel(
    Guid StrategyId,
    string Title,
    string? Description = null,
    Guid? ResponsibleId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? CompletionCriteria = null,
    string Status = "NotStarted",
    int Percent = 0);

public sealed record UpdatePdiPhaseViewModel(
    string Title,
    string? Description = null,
    Guid? ResponsibleId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? CompletionCriteria = null,
    string Status = "NotStarted",
    int Percent = 0);

/// <summary>
/// Validação da fase pelo gestor (§15 — Fase 5, ADR-035):
/// <c>Decision</c> "Validated"/"Rejected" (converter explícito, como a
/// revisão de evidência); <c>Reason</c> obrigatório quando Rejected (a
/// regra fica no handler — 400 pt-BR).
/// </summary>
public sealed record ValidatePdiPhaseViewModel(
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<PdiPhaseValidationDecision>))]
    PdiPhaseValidationDecision Decision,
    string? Reason = null);

public sealed record CreatePdiActivityViewModel(
    Guid PhaseId,
    string Title,
    DateOnly? DueDate = null);

public sealed record UpdatePdiActivityViewModel(
    string Title,
    DateOnly? DueDate = null,
    bool Completed = false);
