namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Upsert do PDI (PUT /api/v1/pdi/{userId}) — 1 plano vigente por
/// usuário. <c>Status</c> aceita case-insensitive ("active"…); o
/// domínio valida transição e responde 400 pt-BR quando inválido
/// (ciclo de 9 estados, ADR-029). <c>TargetDate</c>/<c>PeriodStart</c>/
/// <c>PeriodEnd</c> no formato "yyyy-MM-dd" (DateOnly). <c>ManagerId</c>
/// e períodos são aplicados apenas pelo Admin; <c>Reason</c> registra o
/// motivo da transição no timeline.
/// </summary>
public sealed record UpsertPdiViewModel(
    string Strategy,
    DateOnly TargetDate,
    string Status,
    Guid? ManagerId = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    string? Reason = null);
