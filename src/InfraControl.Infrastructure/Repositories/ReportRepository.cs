using InfraControl.Application.Reports.Transversal;
using InfraControl.Application.Reports.Transversal.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Read-model do relatório transversal (ADR-049) — contagens por status nos
/// 5 agregados + tempo médio de resolução de incidente, com filtro de
/// período sobre <c>CreateDateTime</c>. Agregação no banco (GROUP BY), não
/// em memória. Registro automático por convenção (F0-T024).
/// </summary>
public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public async Task<TransversalReportDto> GetSnapshotAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var lower = from?.ToDateTime(TimeOnly.MinValue);
        var upper = to?.ToDateTime(TimeOnly.MaxValue);

        var pdiQuery = db.IndividualDevelopmentPlans.AsNoTracking();
        var taskQuery = db.Tasks.AsNoTracking();
        var knowledgeQuery = db.KnowledgeArticles.AsNoTracking();
        var projectQuery = db.Projects.AsNoTracking();
        var incidentQuery = db.Incidents.AsNoTracking();

        if (lower is not null)
        {
            pdiQuery = pdiQuery.Where(e => e.CreateDateTime >= lower.Value);
            taskQuery = taskQuery.Where(e => e.CreateDateTime >= lower.Value);
            knowledgeQuery = knowledgeQuery.Where(e => e.CreateDateTime >= lower.Value);
            projectQuery = projectQuery.Where(e => e.CreateDateTime >= lower.Value);
            incidentQuery = incidentQuery.Where(e => e.CreateDateTime >= lower.Value);
        }

        if (upper is not null)
        {
            pdiQuery = pdiQuery.Where(e => e.CreateDateTime <= upper.Value);
            taskQuery = taskQuery.Where(e => e.CreateDateTime <= upper.Value);
            knowledgeQuery = knowledgeQuery.Where(e => e.CreateDateTime <= upper.Value);
            projectQuery = projectQuery.Where(e => e.CreateDateTime <= upper.Value);
            incidentQuery = incidentQuery.Where(e => e.CreateDateTime <= upper.Value);
        }

        var pdiByStatus = await pdiQuery
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var pdiTotal = pdiByStatus.Sum(x => x.Count);

        var taskByState = await taskQuery
            .GroupBy(e => e.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var taskTotal = taskByState.Sum(x => x.Count);
        var taskCompleted = taskByState
            .Where(x => x.State is TaskState.Closed or TaskState.Resolved)
            .Sum(x => x.Count);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var taskOverdue = await taskQuery.CountAsync(
            e => e.DueDate != null
                 && e.DueDate.Value < today
                 && e.State != TaskState.Closed
                 && e.State != TaskState.Resolved
                 && e.State != TaskState.Cancelled,
            cancellationToken);

        var knowledgeTotal = await knowledgeQuery.CountAsync(cancellationToken);
        var knowledgePublished = await knowledgeQuery.CountAsync(
            e => e.Status == KnowledgeArticleStatus.Published, cancellationToken);

        var projectByStatus = await projectQuery
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var incidentTotal = await incidentQuery.CountAsync(cancellationToken);
        var resolvedIncidents = await incidentQuery
            .Where(e => e.Status == IncidentStatus.Resolved && e.ResolvedAt != null)
            .Select(e => new { e.ResolvedAt, e.CreateDateTime })
            .ToListAsync(cancellationToken);
        var averageResolutionHours = resolvedIncidents.Count == 0
            ? (double?)null
            : resolvedIncidents
                .Average(x => (x.ResolvedAt!.Value - x.CreateDateTime).TotalHours);

        return new TransversalReportDto(
            From: from,
            To: to,
            Pdi: new PdiReportBlock(
                Total: pdiTotal,
                ByStatus: pdiByStatus.ToDictionary(
                    x => x.Status.ToString(), x => x.Count),
                Completed: pdiByStatus
                    .Where(x => x.Status == PdiStatus.Completed)
                    .Sum(x => x.Count)),
            Tasks: new TaskReportBlock(
                Total: taskTotal,
                ByState: taskByState.ToDictionary(
                    x => x.State.ToString(), x => x.Count),
                Completed: taskCompleted,
                Overdue: taskOverdue),
            Knowledge: new KnowledgeReportBlock(
                Total: knowledgeTotal,
                Published: knowledgePublished,
                Draft: knowledgeTotal - knowledgePublished),
            Projects: new ProjectReportBlock(
                Total: projectByStatus.Sum(x => x.Count),
                ByStatus: projectByStatus.ToDictionary(
                    x => x.Status.ToString(), x => x.Count)),
            Incidents: new IncidentReportBlock(
                Total: incidentTotal,
                Resolved: resolvedIncidents.Count,
                AverageResolutionHours: averageResolutionHours));
    }
}
