namespace InfraControl.Application.Pdi;

using InfraControl.Domain.Enums;

/// <summary>
/// PDI (Plano de Desenvolvimento Individual) — espelha o formato do
/// <c>TaskDto</c>: enum vira string ("Active"…) no contrato JSON
/// (ciclo de vida de 9 estados, ADR-029).
/// </summary>
public sealed record PdiDto(
    Guid UserId,
    string OwnerName,
    string OwnerEmail,
    string Strategy,
    DateOnly TargetDate,
    string Status,
    Guid? ManagerId,
    string? ManagerName,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateTime UpdateDateTime)
{
    internal static PdiDto From(
        Domain.Entities.IndividualDevelopmentPlan plan,
        string ownerName,
        string ownerEmail,
        string? managerName = null)
        => new(
            plan.UserId,
            ownerName,
            ownerEmail,
            plan.Strategy,
            plan.TargetDate,
            plan.Status.ToString(),
            plan.ManagerId,
            managerName,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.UpdateDateTime);
}

/// <summary>
/// Entrada do timeline do PDI (GET /pdi/{userId}/history) — tipo e
/// status como string, ator com nome resolvido (null = removido).
/// </summary>
public sealed record PdiHistoryDto(
    Guid Id,
    string Type,
    Guid ActorId,
    string? ActorName,
    string? FromStatus,
    string? ToStatus,
    string? Reason,
    string? Detail,
    DateTime OccurredAt)
{
    internal static PdiHistoryDto From(
        Domain.Entities.PdiHistoryEntry entry,
        string? actorName)
        => new(
            entry.Id,
            entry.Type.ToString(),
            entry.ActorId,
            actorName,
            entry.FromStatus?.ToString(),
            entry.ToStatus?.ToString(),
            entry.Reason,
            entry.Detail,
            entry.CreateDateTime);
}

/// <summary>
/// Avaliação do PDI (§15.3 — Fase 6): uma linha por lado
/// (Self = autoavaliação, Manager = avaliação do gestor).
/// </summary>
public sealed record PdiAssessmentDto(
    Guid Id,
    Guid PlanUserId,
    string Side,
    Guid AuthorId,
    string? AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    internal static PdiAssessmentDto From(
        Domain.Entities.PdiAssessment assessment,
        Guid planUserId,
        string? authorName)
        => new(
            assessment.Id,
            planUserId,
            assessment.Side.ToString(),
            assessment.AuthorId,
            authorName,
            assessment.Content,
            assessment.CreateDateTime,
            assessment.UpdateDateTime);
}

/// <summary>Resultado final do PDI (§16 — Fase 6) — único por plano.</summary>
public sealed record PdiResultDto(
    Guid Id,
    Guid PlanUserId,
    string Outcome,
    string? Notes,
    Guid FinalizedById,
    string? FinalizedByName,
    DateOnly FinalizedAt)
{
    internal static PdiResultDto From(
        Domain.Entities.PdiResult result,
        Guid planUserId,
        string? finalizedName)
        => new(
            result.Id,
            planUserId,
            result.Outcome.ToString(),
            result.Notes,
            result.FinalizedById,
            finalizedName,
            result.FinalizedAt);
}

/// <summary>
/// Avaliação final do PDI (GET /pdi/{userId}/evaluation, §16): plano +
/// as duas avaliações + resultado — base da comparação
/// inicial/durante/final no front.
/// </summary>
public sealed record PdiEvaluationDto(
    Guid UserId,
    string Status,
    string Strategy,
    DateOnly TargetDate,
    Guid? ManagerId,
    string? ManagerName,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    PdiAssessmentDto? SelfAssessment,
    PdiAssessmentDto? ManagerAssessment,
    PdiResultDto? Result);

// ------------------------------------------------------------------
// Dashboards (Fase 7 — docs/30 §18, §19, §20, ADR-037)
// ------------------------------------------------------------------

/// <summary>Linha de fase do dashboard do colaborador (§18 — ✓ ● ○).</summary>
public sealed record PdiDashboardPhaseDto(
    Guid Id,
    string Title,
    string Status);

/// <summary>
/// §18 — "Meu PDI": progresso médio das fases, resumo das fases, próximo
/// prazo (menor EndDate entre fases não concluídas), pendências
/// (impedimentos abertos) e totais de evidências/check-ins.
/// </summary>
public sealed record PdiDashboardDto(
    Guid UserId,
    string Status,
    int ProgressPercent,
    IReadOnlyList<PdiDashboardPhaseDto> Phases,
    DateOnly? NextDueDate,
    int OpenImpediments,
    int TotalEvidences,
    int TotalCheckIns);

/// <summary>Plano gerenciado com a classificação do §19 (base do "acesso direto").</summary>
public sealed record PdiManagerPlanDto(
    Guid UserId,
    string OwnerName,
    string Status,
    DateOnly TargetDate,
    bool Overdue,
    bool NeedsAttention);

/// <summary>
/// §19 — "Meus PDIs": classificação 🟢/🟡/🔴 (atrasado > atenção >
/// prazo), fases vencendo em até 7 dias, impedimentos abertos,
/// evidências aguardando validação e a lista de casos para abrir.
/// Plano concluído/cancelado conta como "dentro do prazo" (nada a fazer).
/// </summary>
public sealed record PdiManagerDashboardDto(
    int TotalPlans,
    int OnTrack,
    int NeedsAttention,
    int Overdue,
    int PhasesDueSoon,
    int OpenImpediments,
    int PendingEvidences,
    IReadOnlyList<PdiManagerPlanDto> Plans);

/// <summary>
/// §20 — visão administrativa: contagens por status de plano, taxa de
/// conclusão e agregados de fases/check-ins/evidências/impedimentos.
/// "Em andamento" = qualquer status exceto Concluído/Replanejado/Cancelado.
/// </summary>
public sealed record PdiAdminDashboardDto(
    int TotalPlans,
    int CompletedPlans,
    int InProgressPlans,
    int ReplannedPlans,
    int CancelledPlans,
    int CompletionRatePercent,
    int CompletedPhases,
    int TotalCheckIns,
    int TotalEvidences,
    int TotalImpediments);
