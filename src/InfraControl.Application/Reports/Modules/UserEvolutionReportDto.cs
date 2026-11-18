namespace InfraControl.Application.Reports.Modules;

// ── Evolução por usuário (desempenho) ─────────────────────────────────────

/// <summary>
/// Relatório de evolução/desempenho por usuário (pedido do usuário):
/// consolida por pessoa o que ela produziu no período — tasks
/// (criadas/concluídas/atrasadas/em andamento), projetos (liderados,
/// membro, concluídos), PDI (status, progresso, prazo) e artigos de
/// conhecimento. Admin-only (dados de toda a organização).
/// </summary>
public sealed record UserEvolutionReportDto(
    DateOnly? From,
    DateOnly? To,
    int TotalUsers,
    UserEvolutionTotalsDto Totals,
    IReadOnlyList<UserEvolutionRowDto> Users);

public sealed record UserEvolutionTotalsDto(
    int TasksCreated,
    int TasksCompleted,
    int TasksOverdue,
    int ProjectsCompleted,
    int PdisCompleted,
    int KnowledgeArticles);

public sealed record UserEvolutionRowDto(
    Guid UserId,
    string Name,
    string Email,
    int TasksCreated,
    int TasksCompleted,
    int TasksOverdue,
    int TasksInProgress,
    int ProjectsLed,
    int ProjectsMember,
    int ProjectsCompleted,
    string? PdiStatus,
    int? PdiProgressPercent,
    DateOnly? PdiTargetDate,
    int KnowledgeArticles);
