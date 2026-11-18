using InfraControl.Application.Reports.Modules;
using InfraControl.Application.Reports.Modules.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F25-T004 — relatórios estruturados por módulo (docs/33 §9–§12). Leitura
/// composta no molde de <c>ReportRepository</c> (ADR-049), agregação no
/// banco, sem armazenamento novo. Gaps documentados: "indicadores
/// antes/depois" (§9 impacto) e "geradas automaticamente" (§11) não têm
/// sinal no domínio.
/// </summary>
public sealed class ModuleReportsRepository(AppDbContext db) : IModuleReportsRepository
{
    private static readonly TaskKind[] ImprovementKinds =
    [
        TaskKind.Preventive, TaskKind.Corrective,
        TaskKind.Maintenance, TaskKind.Change,
    ];

    // ── §9 Tasks ──────────────────────────────────────────────────────────

    public async Task<TasksReportDto> GetTasksReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (lower, upper) = Bounds(from, to);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var scope = Scope(db.Tasks.AsNoTracking(), lower, upper);

        var total = await scope.CountAsync(cancellationToken);
        var completed = await scope.CountAsync(
            t => t.State == TaskState.Resolved || t.State == TaskState.Closed,
            cancellationToken);
        var overdue = await scope.CountAsync(
            t => t.DueDate != null
                 && t.DueDate.Value < today
                 && t.State != TaskState.Closed
                 && t.State != TaskState.Resolved
                 && t.State != TaskState.Cancelled,
            cancellationToken);
        var blocked = await scope.CountAsync(
            t => t.State == TaskState.Blocked, cancellationToken);

        var principal = await scope
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreateDateTime)
            .Take(10)
            .Select(t => new TaskHeadlineDto(
                t.Id, t.Title, t.Priority.ToString(), t.State.ToString()))
            .ToListAsync(cancellationToken);

        var responsibles = await BuildResponsiblesAsync(scope, cancellationToken);

        var taskIds = scope.Select(t => t.Id);
        var difficulties = await db.TaskImpediments.AsNoTracking()
            .CountAsync(i => taskIds.Contains(i.TaskId) && i.ResolvedAt == null, cancellationToken);
        var replannings = await db.TaskHistories.AsNoTracking()
            .CountAsync(
                h => taskIds.Contains(h.TaskId) && h.Action == TaskHistoryAction.Replanned,
                cancellationToken);
        var withResult = await scope.CountAsync(
            t => t.Result != null && t.Result != "", cancellationToken);

        var implementacoes = await scope.CountAsync(
            t => t.Kind == TaskKind.Change, cancellationToken);
        var preventivas = await scope.CountAsync(
            t => t.Kind == TaskKind.Preventive, cancellationToken);
        var automacoes = await scope.CountAsync(
            t => t.Kind == TaskKind.Monitoring, cancellationToken);
        var melhorias = await scope.CountAsync(
            t => ImprovementKinds.Contains(t.Kind), cancellationToken);

        return new TasksReportDto(
            from, to,
            new TasksReportSummary(
                total, completed, overdue, blocked, Percent(completed, total)),
            new TasksReportExecution(
                principal, responsibles, difficulties, replannings, withResult),
            new TasksReportImprovements(
                implementacoes, preventivas, automacoes, melhorias),
            new TasksReportImpact(withResult));
    }

    private async Task<IReadOnlyList<NameCountDto>> BuildResponsiblesAsync(
        IQueryable<Domain.Entities.TaskItem> scope,
        CancellationToken cancellationToken)
    {
        var groups = await scope
            .GroupBy(t => t.ResponsibleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var userIds = groups.Where(g => g.Key != null).Select(g => g.Key!.Value).ToList();
        var names = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.DomainUsers.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        return groups
            .OrderByDescending(g => g.Count)
            .Select(g => new NameCountDto(
                g.Key is null ? null : names.GetValueOrDefault(g.Key.Value), g.Count))
            .ToList();
    }

    // ── §10 PDI ───────────────────────────────────────────────────────────

    public async Task<PdiReportDto> GetPdiReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (lower, upper) = Bounds(from, to);
        var plans = Scope(db.IndividualDevelopmentPlans.AsNoTracking(), lower, upper);
        var ownerIds = await plans.Select(p => p.UserId).Distinct().ToListAsync(cancellationToken);

        string? colaborador = null;
        if (ownerIds.Count == 1)
        {
            colaborador = await db.DomainUsers.AsNoTracking()
                .Where(u => u.Id == ownerIds[0])
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var objectives = await db.PdiObjectives.AsNoTracking()
            .Where(o => ownerIds.Contains(o.UserId))
            .ToListAsync(cancellationToken);
        var objectiveIds = objectives.Select(o => o.Id).ToList();

        var strategies = await db.PdiStrategies.AsNoTracking()
            .Where(s => objectiveIds.Contains(s.ObjectiveId))
            .ToListAsync(cancellationToken);
        var strategyIds = strategies.Select(s => s.Id).ToList();

        var phases = await db.PdiPhases.AsNoTracking()
            .Where(p => strategyIds.Contains(p.StrategyId))
            .ToListAsync(cancellationToken);
        var phaseIds = phases.Select(p => p.Id).ToList();

        var progress = phaseIds.Count == 0
            ? 0
            : (int)Math.Round(phases.Average(p => p.Percent));

        var checkIns = await db.PdiCheckIns.AsNoTracking()
            .CountAsync(c => phaseIds.Contains(c.PhaseId), cancellationToken);
        var difficulties = await db.PdiImpediments.AsNoTracking()
            .CountAsync(
                i => phaseIds.Contains(i.PhaseId) && i.Status == PdiImpedimentStatus.Open,
                cancellationToken);
        var evidences = await db.PdiEvidences.AsNoTracking()
            .CountAsync(e => phaseIds.Contains(e.PhaseId), cancellationToken);
        var feedbacks = await db.PdiFeedbacks.AsNoTracking()
            .CountAsync(f => phaseIds.Contains(f.PhaseId), cancellationToken);

        return new PdiReportDto(
            from, to, colaborador,
            objectives.Count, strategies.Count, phases.Count, progress,
            checkIns, difficulties, evidences, feedbacks);
    }

    // ── §11 Knowledge ─────────────────────────────────────────────────────

    public async Task<KnowledgeReportDto> GetKnowledgeReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (lower, upper) = Bounds(from, to);
        var todayUtc = ReportPeriodBounds.StartOfDayUtc(DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var scope = Scope(db.KnowledgeArticles.AsNoTracking(), lower, upper);
        var articleIds = scope.Select(a => a.Id);

        var created = await scope.CountAsync(cancellationToken);
        var published = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Published, cancellationToken);
        var outdated = await scope.CountAsync(
            a => a.NextReviewAt != null && a.NextReviewAt.Value < todayUtc, cancellationToken);

        var originGroups = await db.KnowledgeArticleOrigins.AsNoTracking()
            .Where(o => articleIds.Contains(o.ArticleId))
            .GroupBy(o => o.OriginType)
            .Select(g => new NameCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        var taskLinks = await db.TaskKnowledgeArticles.AsNoTracking()
            .Where(l => articleIds.Contains(l.ArticleId))
            .Select(l => l.ArticleId)
            .ToListAsync(cancellationToken);
        var incidentLinks = await db.KnowledgeArticleIncidents.AsNoTracking()
            .Where(l => articleIds.Contains(l.ArticleId))
            .Select(l => l.ArticleId)
            .ToListAsync(cancellationToken);
        var usageLinks = taskLinks.Count + incidentLinks.Count;

        var useCounts = taskLinks.Concat(incidentLinks)
            .GroupBy(id => id)
            .Select(g => new { Id = g.Key, Uses = g.Count() })
            .OrderByDescending(x => x.Uses)
            .Take(10)
            .ToList();
        var titles = useCounts.Count == 0
            ? new Dictionary<Guid, string>()
            : await scope.Where(a => useCounts.Select(u => u.Id).Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Title, cancellationToken);
        var mostUsed = useCounts
            .Select(u => new KnowledgeUsageDto(
                u.Id, titles.GetValueOrDefault(u.Id, string.Empty), u.Uses))
            .ToList();

        var versions = await db.KnowledgeArticleVersions.AsNoTracking()
            .CountAsync(v => articleIds.Contains(v.ArticleId), cancellationToken);

        return new KnowledgeReportDto(
            from, to, created, published, originGroups,
            usageLinks, mostUsed, outdated, versions);
    }

    // ── §12 Gerencial ─────────────────────────────────────────────────────

    public async Task<GerencialReportDto> GetGerencialReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var tasks = await GetTasksReportAsync(from, to, cancellationToken);
        var pdi = await GetPdiReportAsync(from, to, cancellationToken);
        var knowledge = await GetKnowledgeReportAsync(from, to, cancellationToken);

        var (lower, upper) = Bounds(from, to);
        var taskImpediments = await Scope(db.TaskImpediments.AsNoTracking(), lower, upper)
            .Where(i => i.ResolvedAt == null)
            .Select(i => i.Description)
            .Take(5)
            .ToListAsync(cancellationToken);
        var pdiImpediments = await db.PdiImpediments.AsNoTracking()
            .Where(i => i.Status == PdiImpedimentStatus.Open)
            .Select(i => i.Description ?? i.Type.ToString())
            .Take(5)
            .ToListAsync(cancellationToken);

        var atencao = new List<string>();
        if (tasks.Summary.Overdue > 0)
            atencao.Add($"{tasks.Summary.Overdue} task(s) atrasada(s).");
        if (tasks.Summary.Blocked > 0)
            atencao.Add($"{tasks.Summary.Blocked} task(s) bloqueada(s).");
        if (pdi.Difficulties > 0)
            atencao.Add($"{pdi.Difficulties} dificuldade(s) de PDI em aberto.");
        if (knowledge.Outdated > 0)
            atencao.Add($"{knowledge.Outdated} artigo(s) desatualizado(s).");

        return new GerencialReportDto(
            from, to,
            tasks.Summary,
            tasks.Improvements.Total,
            new PdiReportSummary(
                await Scope(db.IndividualDevelopmentPlans.AsNoTracking(), lower, upper)
                    .CountAsync(cancellationToken),
                await Scope(db.IndividualDevelopmentPlans.AsNoTracking(), lower, upper)
                    .CountAsync(p => p.Status == PdiStatus.Completed, cancellationToken),
                pdi.ProgressPercent),
            new KnowledgeReportSummary(knowledge.Created, knowledge.UsageLinks),
            taskImpediments.Concat(pdiImpediments).ToList(),
            atencao);
    }

    // ── Evolução por usuário ──────────────────────────────────────────────

    /// <summary>
    /// Desempenho por usuário no período: tasks por responsável, projetos
    /// (líder/membro/concluído), PDI (status + progresso médio das fases)
    /// e artigos de conhecimento por autor. Agregação em memória após
    /// projeções mínimas — volumes de RH são pequenos e as junções
    /// PDI→fase não traduzem bem para GROUP BY.
    /// </summary>
    public async Task<UserEvolutionReportDto> GetUserEvolutionReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (lower, upper) = Bounds(from, to);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var users = await db.DomainUsers.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken);
        var userIds = users.Select(u => u.Id).ToList();

        // ── Tasks por responsável ──
        var taskRows = await Scope(db.Tasks.AsNoTracking(), lower, upper)
            .Where(t => t.ResponsibleId != null && userIds.Contains(t.ResponsibleId.Value))
            .Select(t => new
            {
                UserId = t.ResponsibleId!.Value,
                t.State,
                t.DueDate,
            })
            .ToListAsync(cancellationToken);

        var tasksByUser = taskRows
            .GroupBy(t => t.UserId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Created = g.Count(),
                    Completed = g.Count(t =>
                        t.State == TaskState.Resolved || t.State == TaskState.Closed),
                    Overdue = g.Count(t =>
                        t.DueDate != null && t.DueDate.Value < today
                        && t.State != TaskState.Closed
                        && t.State != TaskState.Resolved
                        && t.State != TaskState.Cancelled),
                    InProgress = g.Count(t =>
                        t.State == TaskState.Open || t.State == TaskState.InProgress
                        || t.State == TaskState.Waiting || t.State == TaskState.Blocked),
                });

        // ── Projetos: líder + membro ──
        var projectRows = await Scope(db.Projects.AsNoTracking(), lower, upper)
            .Select(p => new { p.Id, p.LeaderId, p.Status })
            .ToListAsync(cancellationToken);
        var projectIds = projectRows.Select(p => p.Id).ToList();
        var memberRows = projectIds.Count == 0
            ? []
            : await db.ProjectMembers.AsNoTracking()
                .Where(m => projectIds.Contains(m.ProjectId) && userIds.Contains(m.UserId)
                    && m.Role != ProjectMemberRole.Leader)
                .Select(m => new { m.ProjectId, m.UserId })
                .ToListAsync(cancellationToken);

        var projectsLed = projectRows
            .Where(p => userIds.Contains(p.LeaderId))
            .GroupBy(p => p.LeaderId)
            .ToDictionary(g => g.Key, g => g.Count());
        var projectsCompleted = projectRows
            .Where(p => p.Status == ProjectStatus.Completed)
            .ToList();

        // ── PDI: status + progresso médio das fases ──
        var pdiRows = await db.IndividualDevelopmentPlans.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.Status, p.TargetDate })
            .ToListAsync(cancellationToken);

        var pdiUserIds = pdiRows.Select(p => p.UserId).Distinct().ToList();
        var objectiveRows = pdiUserIds.Count == 0
            ? []
            : await db.PdiObjectives.AsNoTracking()
                .Where(o => pdiUserIds.Contains(o.UserId))
                .Select(o => new { o.Id, o.UserId })
                .ToListAsync(cancellationToken);
        var objectiveToUser = objectiveRows.ToDictionary(o => o.Id, o => o.UserId);
        var objectiveIds = objectiveRows.Select(o => o.Id).ToList();

        var strategyRows = objectiveIds.Count == 0
            ? []
            : await db.PdiStrategies.AsNoTracking()
                .Where(s => objectiveIds.Contains(s.ObjectiveId))
                .Select(s => new { s.Id, s.ObjectiveId })
                .ToListAsync(cancellationToken);
        var strategyToUser = strategyRows.ToDictionary(
            s => s.Id,
            s => objectiveToUser.GetValueOrDefault(s.ObjectiveId));
        var strategyIds = strategyRows.Select(s => s.Id).ToList();

        var phaseRows = strategyIds.Count == 0
            ? []
            : await db.PdiPhases.AsNoTracking()
                .Where(p => strategyIds.Contains(p.StrategyId))
                .Select(p => new { p.StrategyId, p.Percent })
                .ToListAsync(cancellationToken);

        var pdiProgress = phaseRows
            .GroupBy(p => strategyToUser.GetValueOrDefault(p.StrategyId))
            .Where(g => g.Key != Guid.Empty)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Round(g.Average(p => (double)p.Percent),
                    MidpointRounding.AwayFromZero));

        var pdiByUser = pdiRows
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        // ── Knowledge por autor ──
        var knowledgeGroups = await Scope(db.KnowledgeArticles.AsNoTracking(), lower, upper)
            .Where(a => a.AuthorId != null && userIds.Contains(a.AuthorId.Value))
            .GroupBy(a => a.AuthorId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var knowledgeByUser = knowledgeGroups.ToDictionary(g => g.UserId, g => g.Count);

        var rows = users.Select(user =>
        {
            tasksByUser.TryGetValue(user.Id, out var tasks);
            var ledProjects = projectsLed.GetValueOrDefault(user.Id);
            var memberProjects = memberRows
                .Where(m => m.UserId == user.Id)
                .Select(m => m.ProjectId)
                .Distinct()
                .Count();
            var completedProjects = projectsCompleted.Count(p =>
                p.LeaderId == user.Id
                || memberRows.Any(m => m.ProjectId == p.Id && m.UserId == user.Id));
            pdiByUser.TryGetValue(user.Id, out var pdi);

            return new UserEvolutionRowDto(
                user.Id,
                user.DisplayName,
                user.Email,
                tasks?.Created ?? 0,
                tasks?.Completed ?? 0,
                tasks?.Overdue ?? 0,
                tasks?.InProgress ?? 0,
                ledProjects,
                memberProjects,
                completedProjects,
                pdi?.Status.ToString(),
                pdi is null ? null : pdiProgress.GetValueOrDefault(user.Id),
                pdi?.TargetDate,
                knowledgeByUser.GetValueOrDefault(user.Id));
        }).ToList();

        var totals = new UserEvolutionTotalsDto(
            taskRows.Count,
            taskRows.Count(t =>
                t.State == TaskState.Resolved || t.State == TaskState.Closed),
            taskRows.Count(t =>
                t.DueDate != null && t.DueDate.Value < today
                && t.State != TaskState.Closed
                && t.State != TaskState.Resolved
                && t.State != TaskState.Cancelled),
            projectsCompleted.Count,
            pdiRows.Count(p => p.Status == PdiStatus.Completed),
            knowledgeGroups.Sum(g => g.Count));

        return new UserEvolutionReportDto(from, to, rows.Count, totals, rows);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static (DateTime? Lower, DateTime? Upper) Bounds(DateOnly? from, DateOnly? to)
        => ReportPeriodBounds.Between(from, to);

    private static IQueryable<T> Scope<T>(
        IQueryable<T> query, DateTime? lower, DateTime? upper)
        where T : Domain.Common.Entities.Entity
    {
        if (lower is not null)
            query = query.Where(e => e.CreateDateTime >= lower.Value);
        if (upper is not null)
            query = query.Where(e => e.CreateDateTime <= upper.Value);
        return query;
    }

    private static int Percent(int part, int total) =>
        total == 0 ? 0 : (int)Math.Round(100.0 * part / total, MidpointRounding.AwayFromZero);
}
