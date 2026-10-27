using InfraControl.Application.Reports.Dashboards;
using InfraControl.Application.Reports.Dashboards.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F25-T003 — dashboards por papel (docs/33 §6–§8). Leitura composta no
/// molde de <c>ReportRepository</c> (ADR-049): agregação no banco, sem
/// armazenamento novo. Colaborador escopado ao usuário; gestor = visão
/// operacional ampla (domínio sem subordinação/equipe); administrativo com
/// filtros de período/responsável/tipo (domínio sem setor/equipe).
/// </summary>
public sealed class ReportDashboardsRepository(AppDbContext db) : IReportDashboardsRepository
{
    private static readonly TaskKind[] ImprovementKinds =
    [
        TaskKind.Preventive, TaskKind.Corrective,
        TaskKind.Maintenance, TaskKind.Change,
    ];

    // ── §6 Colaborador ────────────────────────────────────────────────────

    public async Task<ColaboradorDashboardDto> GetColaboradorAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var myTasks = await BuildColaboradorTasksAsync(userId, today, cancellationToken);
        var myPdi = await BuildColaboradorPdiAsync(userId, today, cancellationToken);
        var myKnowledge = await BuildColaboradorKnowledgeAsync(userId, cancellationToken);

        return new ColaboradorDashboardDto(
            userId == Guid.Empty ? null : userId,
            myTasks, myPdi, myKnowledge);
    }

    private async Task<ColaboradorTasksBlock> BuildColaboradorTasksAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var scope = db.Tasks.AsNoTracking().Where(t => t.ResponsibleId == userId);

        var pending = await scope.CountAsync(
            t => t.State == TaskState.Open
                 || t.State == TaskState.InProgress
                 || t.State == TaskState.Waiting,
            cancellationToken);
        var inProgress = await scope.CountAsync(
            t => t.State == TaskState.InProgress, cancellationToken);
        var overdue = await scope.CountAsync(
            t => t.DueDate != null
                 && t.DueDate.Value < today
                 && t.State != TaskState.Closed
                 && t.State != TaskState.Resolved
                 && t.State != TaskState.Cancelled,
            cancellationToken);
        var completed = await scope.CountAsync(
            t => t.State == TaskState.Resolved || t.State == TaskState.Closed,
            cancellationToken);

        return new ColaboradorTasksBlock(pending, inProgress, overdue, completed);
    }

    private async Task<ColaboradorPdiBlock> BuildColaboradorPdiAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var hasPlan = await db.IndividualDevelopmentPlans.AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        var phaseIds =
            from ph in db.PdiPhases.AsNoTracking()
            join st in db.PdiStrategies.AsNoTracking() on ph.StrategyId equals st.Id
            join ob in db.PdiObjectives.AsNoTracking() on st.ObjectiveId equals ob.Id
            where ob.UserId == userId
            select ph.Id;

        var phaseIdList = await phaseIds.ToListAsync(cancellationToken);
        var phases = phaseIdList.Count;

        var progressPercent = 0;
        if (phases > 0)
        {
            var percents = await db.PdiPhases.AsNoTracking()
                .Where(p => phaseIdList.Contains(p.Id))
                .Select(p => p.Percent)
                .ToListAsync(cancellationToken);
            progressPercent = percents.Count == 0
                ? 0
                : (int)Math.Round(percents.Average());
        }

        var nextActivities = await db.PdiActivities.AsNoTracking()
            .CountAsync(
                a => phaseIdList.Contains(a.PhaseId)
                     && a.DueDate != null
                     && a.DueDate.Value >= today,
                cancellationToken);
        var checkIns = await db.PdiCheckIns.AsNoTracking()
            .CountAsync(c => phaseIdList.Contains(c.PhaseId), cancellationToken);
        var difficulties = await db.PdiImpediments.AsNoTracking()
            .CountAsync(
                i => phaseIdList.Contains(i.PhaseId)
                     && i.Status == PdiImpedimentStatus.Open,
                cancellationToken);

        return new ColaboradorPdiBlock(
            hasPlan, progressPercent, phases, nextActivities, checkIns, difficulties);
    }

    private async Task<ColaboradorKnowledgeBlock> BuildColaboradorKnowledgeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var scope = db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.AuthorId == userId);

        var created = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Draft, cancellationToken);
        var inReview = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.InReview, cancellationToken);
        var published = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Published, cancellationToken);

        return new ColaboradorKnowledgeBlock(created, inReview, published);
    }

    // ── §7 Gestor ─────────────────────────────────────────────────────────

    public async Task<GestorDashboardDto> GetGestorAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var tasks = await BuildGestorTasksAsync(cancellationToken);
        var pdi = await BuildGestorPdiAsync(cancellationToken);
        var knowledge = await BuildGestorKnowledgeAsync(today, cancellationToken);

        var upcomingDeadlines = await BuildUpcomingDeadlinesAsync(today, cancellationToken);
        var openTaskImpediments = await db.TaskImpediments.AsNoTracking()
            .CountAsync(i => i.ResolvedAt == null, cancellationToken);
        var openPdiImpediments = await db.PdiImpediments.AsNoTracking()
            .CountAsync(i => i.Status == PdiImpedimentStatus.Open, cancellationToken);

        var priorityTasks = await BuildPriorityTasksAsync(cancellationToken);
        var plansNeedingFollowUp = await CountPlansWithOverduePhaseAsync(today, cancellationToken);
        var knowledgePendingApproval = await db.KnowledgeArticles.AsNoTracking()
            .CountAsync(a => a.Status == KnowledgeArticleStatus.InReview, cancellationToken);

        return new GestorDashboardDto(
            tasks, pdi, knowledge,
            upcomingDeadlines,
            openTaskImpediments + openPdiImpediments,
            priorityTasks,
            plansNeedingFollowUp,
            knowledgePendingApproval);
    }

    private async Task<GestorTasksBlock> BuildGestorTasksAsync(
        CancellationToken cancellationToken)
    {
        var scope = db.Tasks.AsNoTracking();

        var inProgress = await scope.CountAsync(
            t => t.State == TaskState.InProgress, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var overdue = await scope.CountAsync(
            t => t.DueDate != null
                 && t.DueDate.Value < today
                 && t.State != TaskState.Closed
                 && t.State != TaskState.Resolved
                 && t.State != TaskState.Cancelled,
            cancellationToken);
        var blocked = await scope.CountAsync(
            t => t.State == TaskState.Blocked, cancellationToken);
        var completed = await scope.CountAsync(
            t => t.State == TaskState.Resolved || t.State == TaskState.Closed,
            cancellationToken);
        var improvements = await scope.CountAsync(
            t => ImprovementKinds.Contains(t.Kind), cancellationToken);

        return new GestorTasksBlock(inProgress, overdue, blocked, completed, improvements);
    }

    private async Task<GestorPdiBlock> BuildGestorPdiAsync(
        CancellationToken cancellationToken)
    {
        var scope = db.IndividualDevelopmentPlans.AsNoTracking();

        var active = await scope.CountAsync(
            p => p.Status == PdiStatus.Active || p.Status == PdiStatus.InProgress,
            cancellationToken);
        var attention = await scope.CountAsync(
            p => p.Status == PdiStatus.AwaitingAcceptance
                 || p.Status == PdiStatus.UnderEvaluation,
            cancellationToken);
        var completed = await scope.CountAsync(
            p => p.Status == PdiStatus.Completed, cancellationToken);
        var replanned = await scope.CountAsync(
            p => p.Status == PdiStatus.Replanned, cancellationToken);

        return new GestorPdiBlock(active, attention, completed, replanned);
    }

    private async Task<GestorKnowledgeBlock> BuildGestorKnowledgeAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var scope = db.KnowledgeArticles.AsNoTracking();
        var todayUtc = today.ToDateTime(TimeOnly.MinValue);

        var published = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Published, cancellationToken);
        var inReview = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.InReview, cancellationToken);
        var pending = await scope.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Approved, cancellationToken);
        var outdated = await scope.CountAsync(
            a => a.NextReviewAt != null && a.NextReviewAt.Value < todayUtc,
            cancellationToken);

        return new GestorKnowledgeBlock(published, inReview, pending, outdated);
    }

    private async Task<IReadOnlyList<UpcomingDeadlineDto>> BuildUpcomingDeadlinesAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(7);
        return await db.Tasks.AsNoTracking()
            .Where(t => t.DueDate != null
                        && t.DueDate.Value >= today
                        && t.DueDate.Value <= horizon
                        && t.State != TaskState.Closed
                        && t.State != TaskState.Resolved
                        && t.State != TaskState.Cancelled)
            .OrderBy(t => t.DueDate)
            .Take(10)
            .Select(t => new UpcomingDeadlineDto(t.Id, t.Title, t.DueDate!.Value))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<PriorityTaskDto>> BuildPriorityTasksAsync(
        CancellationToken cancellationToken)
    {
        return await db.Tasks.AsNoTracking()
            .Where(t => (t.Priority == TaskPriority.High || t.Priority == TaskPriority.Critical)
                        && t.State != TaskState.Closed
                        && t.State != TaskState.Resolved
                        && t.State != TaskState.Cancelled)
            .OrderByDescending(t => t.Priority)
            .Take(10)
            .Select(t => new PriorityTaskDto(t.Id, t.Title, t.Priority.ToString()))
            .ToListAsync(cancellationToken);
    }

    private async Task<int> CountPlansWithOverduePhaseAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var phaseRows =
            from ph in db.PdiPhases.AsNoTracking()
            join st in db.PdiStrategies.AsNoTracking() on ph.StrategyId equals st.Id
            where ph.EndDate != null
                  && ph.EndDate.Value < today
                  && ph.Status != PdiPhaseStatus.Completed
            select st.ObjectiveId;

        var objectiveIds = await phaseRows.Distinct().ToListAsync(cancellationToken);
        if (objectiveIds.Count == 0)
            return 0;

        var userIds = await db.PdiObjectives.AsNoTracking()
            .Where(o => objectiveIds.Contains(o.Id))
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return 0;

        return await db.IndividualDevelopmentPlans.AsNoTracking()
            .CountAsync(
                p => userIds.Contains(p.UserId)
                     && (p.Status == PdiStatus.Active || p.Status == PdiStatus.InProgress),
                cancellationToken);
    }

    // ── §8 Administrativo ─────────────────────────────────────────────────

    public async Task<AdministrativoDashboardDto> GetAdministrativoAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? responsibleId,
        TaskKindFilter? kind,
        CancellationToken cancellationToken = default)
    {
        var lower = from?.ToDateTime(TimeOnly.MinValue);
        var upper = to?.ToDateTime(TimeOnly.MaxValue);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var taskQuery = db.Tasks.AsNoTracking();
        if (lower is not null)
            taskQuery = taskQuery.Where(t => t.CreateDateTime >= lower.Value);
        if (upper is not null)
            taskQuery = taskQuery.Where(t => t.CreateDateTime <= upper.Value);
        if (responsibleId is not null)
            taskQuery = taskQuery.Where(t => t.ResponsibleId == responsibleId);
        if (kind is not null)
            taskQuery = taskQuery.Where(t => t.Kind == MapKind(kind.Value));

        var total = await taskQuery.CountAsync(cancellationToken);
        var completed = await taskQuery.CountAsync(
            t => t.State == TaskState.Resolved || t.State == TaskState.Closed,
            cancellationToken);
        var overdue = await taskQuery.CountAsync(
            t => t.DueDate != null
                 && t.DueDate.Value < today
                 && t.State != TaskState.Closed
                 && t.State != TaskState.Resolved
                 && t.State != TaskState.Cancelled,
            cancellationToken);
        var blocked = await taskQuery.CountAsync(
            t => t.State == TaskState.Blocked, cancellationToken);

        var byResponsible = await BuildByResponsibleAsync(taskQuery, cancellationToken);

        var pdiQuery = db.IndividualDevelopmentPlans.AsNoTracking();
        if (lower is not null)
            pdiQuery = pdiQuery.Where(p => p.CreateDateTime >= lower.Value);
        if (upper is not null)
            pdiQuery = pdiQuery.Where(p => p.CreateDateTime <= upper.Value);
        var pdiTotal = await pdiQuery.CountAsync(cancellationToken);
        var pdiCompleted = await pdiQuery.CountAsync(
            p => p.Status == PdiStatus.Completed, cancellationToken);

        var knowledgeQuery = db.KnowledgeArticles.AsNoTracking();
        if (lower is not null)
            knowledgeQuery = knowledgeQuery.Where(a => a.CreateDateTime >= lower.Value);
        if (upper is not null)
            knowledgeQuery = knowledgeQuery.Where(a => a.CreateDateTime <= upper.Value);
        var knowledgeTotal = await knowledgeQuery.CountAsync(cancellationToken);
        var knowledgePublished = await knowledgeQuery.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Published, cancellationToken);
        var knowledgeInReview = await knowledgeQuery.CountAsync(
            a => a.Status == KnowledgeArticleStatus.InReview, cancellationToken);
        var knowledgeDraft = await knowledgeQuery.CountAsync(
            a => a.Status == KnowledgeArticleStatus.Draft, cancellationToken);

        return new AdministrativoDashboardDto(
            from, to,
            new AdministrativoTasksBlock(
                total, completed, overdue, blocked, Percent(completed, total)),
            byResponsible,
            new AdministrativoPdiBlock(
                pdiTotal, pdiCompleted, Percent(pdiCompleted, pdiTotal)),
            new AdministrativoKnowledgeBlock(
                knowledgeTotal, knowledgePublished, knowledgeInReview, knowledgeDraft));
    }

    private async Task<IReadOnlyList<ResponsibleShareDto>> BuildByResponsibleAsync(
        IQueryable<Domain.Entities.TaskItem> taskQuery,
        CancellationToken cancellationToken)
    {
        var groups = await taskQuery
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
            .Select(g => new ResponsibleShareDto(
                g.Key,
                g.Key is null ? null : names.GetValueOrDefault(g.Key.Value),
                g.Count))
            .ToList();
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static TaskKind MapKind(TaskKindFilter filter) => filter switch
    {
        TaskKindFilter.Manual => TaskKind.Manual,
        TaskKindFilter.Incident => TaskKind.Incident,
        TaskKindFilter.Alert => TaskKind.Alert,
        TaskKindFilter.Monitoring => TaskKind.Monitoring,
        TaskKindFilter.Discovery => TaskKind.Discovery,
        TaskKindFilter.Maintenance => TaskKind.Maintenance,
        TaskKindFilter.Change => TaskKind.Change,
        TaskKindFilter.Preventive => TaskKind.Preventive,
        TaskKindFilter.Corrective => TaskKind.Corrective,
        _ => TaskKind.Manual,
    };

    private static int Percent(int part, int total) =>
        total == 0 ? 0 : (int)Math.Round(100.0 * part / total, MidpointRounding.AwayFromZero);
}
