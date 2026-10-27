using InfraControl.Application.Reports.Indicators;
using InfraControl.Application.Reports.Indicators.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F25-T001 — indicadores detalhados de Tasks/PDI/Knowledge (docs/33 §2–§4).
/// Leitura composta no molde de <c>ReportRepository</c> (ADR-049): agregação
/// no banco, filtro de período sobre <c>CreateDateTime</c>, sem
/// armazenamento novo. Resolve nomes de responsável em lote (sem N+1).
/// Registro automático por convenção (F0-T024).
/// </summary>
public sealed class ReportIndicatorsRepository(AppDbContext db) : IReportIndicatorsRepository
{
    public async Task<ReportIndicatorsDto> GetIndicatorsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var lower = from?.ToDateTime(TimeOnly.MinValue);
        var upper = to?.ToDateTime(TimeOnly.MaxValue);

        var taskScope = Scope(db.Tasks.AsNoTracking(), lower, upper);
        var pdiScope = Scope(db.IndividualDevelopmentPlans.AsNoTracking(), lower, upper);
        var knowledgeScope = Scope(db.KnowledgeArticles.AsNoTracking(), lower, upper);

        var tasks = await BuildTaskIndicatorsAsync(taskScope, cancellationToken);
        var pdi = await BuildPdiIndicatorsAsync(pdiScope, cancellationToken);
        var knowledge = await BuildKnowledgeIndicatorsAsync(
            knowledgeScope, db, lower, upper, cancellationToken);

        return new ReportIndicatorsDto(
            From: from,
            To: to,
            Tasks: tasks,
            Pdi: pdi,
            Knowledge: knowledge);
    }

    // ── §2 Tasks ──────────────────────────────────────────────────────────

    private async Task<TaskIndicatorsDto> BuildTaskIndicatorsAsync(
        IQueryable<Domain.Entities.TaskItem> scope,
        CancellationToken cancellationToken)
    {
        var byState = await scope
            .GroupBy(e => e.State)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var total = byState.Sum(x => x.Count);
        var completed = byState
            .Where(x => x.Key is TaskState.Closed or TaskState.Resolved)
            .Sum(x => x.Count);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var overdue = await scope.CountAsync(
            e => e.DueDate != null
                 && e.DueDate.Value < today
                 && e.State != TaskState.Closed
                 && e.State != TaskState.Resolved
                 && e.State != TaskState.Cancelled,
            cancellationToken);
        var blocked = await scope.CountAsync(
            e => e.State == TaskState.Blocked, cancellationToken);
        var withResult = await scope.CountAsync(
            e => e.Result != null && e.Result != "", cancellationToken);

        var byPriority = await scope
            .GroupBy(e => e.Priority)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byKind = await scope
            .GroupBy(e => e.Kind)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Responsável em lote (ids → nomes), sem N+1.
        var byResponsibleRaw = await scope
            .GroupBy(e => e.ResponsibleId)
            .Select(g => new { ResponsibleId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);
        var responsibleIds = byResponsibleRaw
            .Where(x => x.ResponsibleId != null)
            .Select(x => x.ResponsibleId!.Value)
            .Distinct()
            .ToArray();
        var names = responsibleIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.DomainUsers.AsNoTracking()
                .Where(u => responsibleIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var byResponsible = byResponsibleRaw
            .Select(x => new TaskResponsibleShareDto(
                x.ResponsibleId,
                x.ResponsibleId is { } id && names.TryGetValue(id, out var n) ? n : null,
                x.Count,
                Percent(x.Count, total)))
            .ToList();

        // Histórico: replanejamentos e total de atualizações (ações ≠ Created).
        var taskIds = await scope.Select(e => e.Id).ToListAsync(cancellationToken);
        var (replannings, updates) = await CountHistoryAsync(taskIds, cancellationToken);

        var (impediments, impedimentsResolved) = await CountImpedimentsAsync(
            taskIds, cancellationToken);

        return new TaskIndicatorsDto(
            Total: total,
            ByState: ToCountMap(byState.Select(x => (x.Key.ToString(), x.Count))),
            CompletionRatePercent: Percent(completed, total),
            Overdue: overdue,
            Blocked: blocked,
            Replannings: replannings,
            Impediments: impediments,
            ImpedimentsResolved: impedimentsResolved,
            Updates: updates,
            WithResult: withResult,
            ByPriority: ToCountMap(byPriority.Select(x => (x.Key.ToString(), x.Count))),
            ByKind: ToCountMap(byKind.Select(x => (x.Key.ToString(), x.Count))),
            ByResponsible: byResponsible);
    }

    private async Task<(int Replannings, int Updates)> CountHistoryAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
            return (0, 0);

        var replannings = await db.TaskHistories.AsNoTracking()
            .CountAsync(h => taskIds.Contains(h.TaskId)
                             && h.Action == TaskHistoryAction.Replanned,
                cancellationToken);
        var updates = await db.TaskHistories.AsNoTracking()
            .CountAsync(h => taskIds.Contains(h.TaskId)
                             && h.Action != TaskHistoryAction.Created,
                cancellationToken);
        return (replannings, updates);
    }

    private async Task<(int Total, int Resolved)> CountImpedimentsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
            return (0, 0);

        var total = await db.TaskImpediments.AsNoTracking()
            .CountAsync(i => taskIds.Contains(i.TaskId), cancellationToken);
        var resolved = await db.TaskImpediments.AsNoTracking()
            .CountAsync(i => taskIds.Contains(i.TaskId) && i.ResolvedAt != null,
                cancellationToken);
        return (total, resolved);
    }

    // ── §3 PDI ────────────────────────────────────────────────────────────

    private async Task<PdiIndicatorsBlockDto> BuildPdiIndicatorsAsync(
        IQueryable<Domain.Entities.IndividualDevelopmentPlan> scope,
        CancellationToken cancellationToken)
    {
        var byStatus = await scope
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var total = byStatus.Sum(x => x.Count);
        var completed = byStatus
            .Where(x => x.Key == PdiStatus.Completed)
            .Sum(x => x.Count);

        // Desenvolvimento: objetivos e competências distintas trabalhadas.
        // PdiObjective.UserId referencia o DONO do plano (1 plano/usuário),
        // então o match é por UserId do plano, não pelo Id do plano.
        var ownerIds = await scope.Select(e => e.UserId).ToListAsync(cancellationToken);
        var objectivesTotal = ownerIds.Count == 0
            ? 0
            : await db.PdiObjectives.AsNoTracking()
                .CountAsync(o => ownerIds.Contains(o.UserId), cancellationToken);
        var competenciesWorked = ownerIds.Count == 0
            ? 0
            : await db.PdiObjectives.AsNoTracking()
                .Where(o => ownerIds.Contains(o.UserId) && o.CompetencyId != null)
                .Select(o => o.CompetencyId!.Value)
                .Distinct()
                .CountAsync(cancellationToken);

        var (impediments, impedimentsResolved) = await CountPdiImpedimentsAsync(
            ownerIds, cancellationToken);

        return new PdiIndicatorsBlockDto(
            TotalPlans: total,
            ByStatus: ToCountMap(byStatus.Select(x => (x.Key.ToString(), x.Count))),
            CompletedPlans: completed,
            CompletionRatePercent: Percent(completed, total),
            ObjectivesTotal: objectivesTotal,
            CompetenciesWorked: competenciesWorked,
            TotalImpediments: impediments,
            ImpedimentsResolved: impedimentsResolved);
    }

    private async Task<(int Total, int Resolved)> CountPdiImpedimentsAsync(
        IReadOnlyCollection<Guid> ownerIds,
        CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0)
            return (0, 0);

        // Impedimentos vivem sob phases → strategies → objectives(UserId dono).
        var phaseIds = await db.PdiObjectives.AsNoTracking()
            .Where(o => ownerIds.Contains(o.UserId))
            .Join(db.PdiStrategies.AsNoTracking(), o => o.Id, s => s.ObjectiveId,
                (o, s) => new { s.Id })
            .Join(db.PdiPhases.AsNoTracking(), x => x.Id, p => p.StrategyId,
                (x, p) => p.Id)
            .ToListAsync(cancellationToken);
        if (phaseIds.Count == 0)
            return (0, 0);

        var total = await db.PdiImpediments.AsNoTracking()
            .CountAsync(i => phaseIds.Contains(i.PhaseId), cancellationToken);
        var resolved = await db.PdiImpediments.AsNoTracking()
            .CountAsync(i => phaseIds.Contains(i.PhaseId)
                             && i.Status == PdiImpedimentStatus.Resolved,
                cancellationToken);
        return (total, resolved);
    }

    // ── §4 Knowledge ──────────────────────────────────────────────────────

    private async Task<KnowledgeIndicatorsDto> BuildKnowledgeIndicatorsAsync(
        IQueryable<Domain.Entities.KnowledgeArticle> scope,
        AppDbContext context,
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var byStatus = await scope
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var total = byStatus.Sum(x => x.Count);
        int Count(KnowledgeArticleStatus s) =>
            byStatus.Where(x => x.Key == s).Sum(x => x.Count);

        var now = DateTime.UtcNow;
        var pendingReview = await scope.CountAsync(
            e => e.NextReviewAt != null && e.NextReviewAt <= now,
            cancellationToken);

        var articleIds = await scope.Select(e => e.Id).ToListAsync(cancellationToken);

        // Origens (Task/Pdi/Project/Incident) — contagem de vínculos por tipo.
        var byOrigin = articleIds.Count == 0
            ? new Dictionary<string, int>()
            : await context.KnowledgeArticleOrigins.AsNoTracking()
                .Where(o => articleIds.Contains(o.ArticleId))
                .GroupBy(o => o.OriginType)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count,
                    cancellationToken);

        // Evolução: artigos com mais de uma versão.
        var versioned = articleIds.Count == 0
            ? 0
            : await context.KnowledgeArticleVersions.AsNoTracking()
                .Where(v => articleIds.Contains(v.ArticleId))
                .GroupBy(v => v.ArticleId)
                .CountAsync(g => g.Count() > 1, cancellationToken);

        // Tags: flatten em memória (coluna JSON — sem GROUP BY no banco).
        var topTags = await BuildTopTagsAsync(scope, cancellationToken);

        return new KnowledgeIndicatorsDto(
            Total: total,
            ByStatus: ToCountMap(byStatus.Select(x => (x.Key.ToString(), x.Count))),
            Published: Count(KnowledgeArticleStatus.Published),
            InReview: Count(KnowledgeArticleStatus.InReview),
            Draft: Count(KnowledgeArticleStatus.Draft),
            PendingReview: pendingReview,
            Obsolete: Count(KnowledgeArticleStatus.Obsolete),
            Archived: Count(KnowledgeArticleStatus.Archived),
            ByOrigin: byOrigin,
            Versioned: versioned,
            TopTags: topTags);
    }

    private static async Task<IReadOnlyList<KnowledgeTagShareDto>> BuildTopTagsAsync(
        IQueryable<Domain.Entities.KnowledgeArticle> scope,
        CancellationToken cancellationToken)
    {
        var tagLists = await scope
            .Select(e => e.Tags)
            .ToListAsync(cancellationToken);
        return tagLists
            .SelectMany(tags => tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .GroupBy(tag => tag.Trim().ToLowerInvariant())
            .Select(g => new KnowledgeTagShareDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Tag)
            .Take(20)
            .ToList();
    }

    // ── helpers ───────────────────────────────────────────────────────────

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

    private static IReadOnlyDictionary<string, int> ToCountMap(
        IEnumerable<(string Key, int Count)> pairs) =>
        pairs.GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
}
