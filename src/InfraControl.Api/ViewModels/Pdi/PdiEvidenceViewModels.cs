using System.Text.Json.Serialization;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Revisão da evidência (docs/30 §12, Fase 4 — ADR-034): decisão do
/// gestor; <c>RejectionReason</c> obrigatório quando Decision = Rejected
/// (a regra fica no domínio — 400 em pt-BR). O converter explícito aceita
/// "Validated"/"Rejected" (o front envia string) — sem ele o STJ espera
/// número e todo POST de revisão cai em 400 de model binding.
/// </summary>
public sealed record ReviewPdiEvidenceViewModel(
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<PdiEvidenceReviewDecision>))]
    PdiEvidenceReviewDecision Decision,
    string? RejectionReason);
