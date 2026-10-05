namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Upsert do PDI (PUT /api/v1/pdi/{userId}) — 1 plano vigente por usuário.
/// <c>Status</c> aceita case-insensitive ("inprogress"…); domínio valida e
/// responde 400 pt-BR quando inválido. <c>TargetDate</c> no formato
/// "yyyy-MM-dd" (DateOnly).
/// </summary>
public sealed record UpsertPdiViewModel(
    string Strategy,
    DateOnly TargetDate,
    string Status);
