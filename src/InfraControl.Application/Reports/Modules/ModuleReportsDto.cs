namespace InfraControl.Application.Reports.Modules;

// ── §9 Relatório de Tasks ─────────────────────────────────────────────────

/// <summary>
/// F25-T004 §9 — relatório de Tasks por período: resumo, execução, melhorias
/// e impacto. "Indicadores antes/depois" não é modelado no domínio (gap).
/// </summary>
public sealed record TasksReportDto(
    DateOnly? From,
    DateOnly? To,
    TasksReportSummary Summary,
    TasksReportExecution Execution,
    TasksReportImprovements Improvements,
    TasksReportImpact Impact);

public sealed record TasksReportSummary(
    int Quantity,
    int Completed,
    int Overdue,
    int Blocked,
    int CompletionRatePercent);

public sealed record TasksReportExecution(
    IReadOnlyList<TaskHeadlineDto> PrincipalTasks,
    IReadOnlyList<NameCountDto> Responsibles,
    int Difficulties,
    int Replannings,
    int WithResult);

public sealed record TaskHeadlineDto(
    Guid TaskId,
    string Title,
    string Priority,
    string State);

public sealed record NameCountDto(string? Name, int Count);

public sealed record TasksReportImprovements(
    int Implementations,
    int Preventives,
    int Automations,
    int Total);

public sealed record TasksReportImpact(int WithResult);

// ── §10 Relatório de PDI ──────────────────────────────────────────────────

/// <summary>
/// F25-T004 §10 — relatório de PDI: colaborador, objetivos, fases, evolução,
/// check-ins, dificuldades, evidências e feedbacks.
/// </summary>
public sealed record PdiReportDto(
    DateOnly? From,
    DateOnly? To,
    string? Colaborador,
    int Objectives,
    int Strategies,
    int Phases,
    int ProgressPercent,
    int CheckIns,
    int Difficulties,
    int Evidences,
    int Feedbacks);

// ── §11 Relatório de Knowledge ────────────────────────────────────────────

/// <summary>
/// F25-T004 §11 — relatório de Knowledge: criados, publicados, origem,
/// utilização (vínculos), mais acessadas, desatualizadas, versões.
/// "Geradas automaticamente" não tem flag no domínio (gap — K6).
/// </summary>
public sealed record KnowledgeReportDto(
    DateOnly? From,
    DateOnly? To,
    int Created,
    int Published,
    IReadOnlyList<NameCountDto> ByOrigin,
    int UsageLinks,
    IReadOnlyList<KnowledgeUsageDto> MostUsed,
    int Outdated,
    int Versions);

public sealed record KnowledgeUsageDto(
    Guid ArticleId,
    string Title,
    int Uses);

// ── §12 Relatório gerencial ───────────────────────────────────────────────

/// <summary>
/// F25-T004 §12 — relatório gerencial do período (resumo executivo →
/// próximos passos). "Impactos/indicadores antes-depois" não modelados.
/// </summary>
public sealed record GerencialReportDto(
    DateOnly? From,
    DateOnly? To,
    TasksReportSummary Tasks,
    int Improvements,
    PdiReportSummary Pdi,
    KnowledgeReportSummary Knowledge,
    IReadOnlyList<string> PrincipaisImpedimentos,
    IReadOnlyList<string> PontosAtencao);

public sealed record PdiReportSummary(
    int Plans,
    int Completed,
    int ProgressPercent);

public sealed record KnowledgeReportSummary(
    int Created,
    int Used);
