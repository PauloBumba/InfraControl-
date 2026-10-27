namespace InfraControl.Application.Reports.Dashboards;

// ── §6 Dashboard do colaborador (escopo do usuário atual) ─────────────────

/// <summary>
/// F25-T003 §6 — dashboard do colaborador: minhas Tasks, meu PDI e meu
/// conhecimento, escopados ao usuário autenticado.
/// </summary>
public sealed record ColaboradorDashboardDto(
    Guid? UserId,
    ColaboradorTasksBlock MyTasks,
    ColaboradorPdiBlock MyPdi,
    ColaboradorKnowledgeBlock MyKnowledge);

public sealed record ColaboradorTasksBlock(
    int Pending,
    int InProgress,
    int Overdue,
    int Completed);

public sealed record ColaboradorPdiBlock(
    bool HasPlan,
    int ProgressPercent,
    int Phases,
    int NextActivities,
    int CheckIns,
    int Difficulties);

public sealed record ColaboradorKnowledgeBlock(
    int Created,
    int InReview,
    int Published);

// ── §7 Dashboard do gestor (visão operacional ampla) ──────────────────────
// Obs.: o domínio NÃO tem modelo de subordinação/equipe, então o dashboard
// do gestor é a visão operacional consolidada de toda a organização.

/// <summary>
/// F25-T003 §7 — dashboard do gestor: situação de Tasks/PDI/Knowledge +
/// listas de atenção (prazos, impedimentos, prioridades, aprovações).
/// </summary>
public sealed record GestorDashboardDto(
    GestorTasksBlock Tasks,
    GestorPdiBlock Pdi,
    GestorKnowledgeBlock Knowledge,
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines,
    int OpenImpediments,
    IReadOnlyList<PriorityTaskDto> PriorityTasks,
    int PlansNeedingFollowUp,
    int KnowledgePendingApproval);

public sealed record GestorTasksBlock(
    int InProgress,
    int Overdue,
    int Blocked,
    int Completed,
    int Improvements);

public sealed record GestorPdiBlock(
    int Active,
    int Attention,
    int Completed,
    int Replanned);

public sealed record GestorKnowledgeBlock(
    int Published,
    int InReview,
    int Pending,
    int Outdated);

public sealed record UpcomingDeadlineDto(
    Guid TaskId,
    string Title,
    DateOnly DueDate);

public sealed record PriorityTaskDto(
    Guid TaskId,
    string Title,
    string Priority);

// ── §8 Dashboard administrativo (consolidado + filtros) ────────────────────

/// <summary>
/// F25-T003 §8 — visão administrativa consolidada com filtros de período,
/// responsável e tipo. Obs.: o domínio NÃO tem setor/equipe, então os filtros
/// suportados são período, responsável e tipo.
/// </summary>
public sealed record AdministrativoDashboardDto(
    DateOnly? From,
    DateOnly? To,
    AdministrativoTasksBlock Tasks,
    IReadOnlyList<ResponsibleShareDto> ByResponsible,
    AdministrativoPdiBlock Pdi,
    AdministrativoKnowledgeBlock Knowledge);

public sealed record AdministrativoTasksBlock(
    int Total,
    int Completed,
    int Overdue,
    int Blocked,
    int CompletionRatePercent);

public sealed record ResponsibleShareDto(
    Guid? ResponsibleId,
    string? ResponsibleName,
    int Count);

public sealed record AdministrativoPdiBlock(
    int TotalPlans,
    int Completed,
    int CompletionRatePercent);

public sealed record AdministrativoKnowledgeBlock(
    int Total,
    int Published,
    int InReview,
    int Draft);
