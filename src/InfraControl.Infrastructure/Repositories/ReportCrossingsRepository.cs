using System.Linq.Expressions;
using InfraControl.Application.Reports.Crossings;
using InfraControl.Application.Reports.Crossings.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F25-T002 — cruzamentos integrados (docs/33 §5). Lê os vínculos N:N já
/// prontos sem expor agregados: <c>task_incidents</c>,
/// <c>task_knowledge_articles</c>, <c>knowledge_article_origins</c> (Pdi),
/// <c>knowledge_article_incidents</c> e <c>task_pdis</c>. Filtro de período
/// sobre a entidade de origem (<c>CreateDateTime</c>); agregação no banco,
/// sem armazenamento novo (molde ADR-049).
/// </summary>
public sealed class ReportCrossingsRepository(AppDbContext db) : IReportCrossingsRepository
{
    public async Task<ReportCrossingsDto> GetCrossingsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var lower = from?.ToDateTime(TimeOnly.MinValue);
        var upper = to?.ToDateTime(TimeOnly.MaxValue);

        var incidentsToTasks = await BuildIncidentsToTasksAsync(lower, upper, cancellationToken);
        var tasksToKnowledge = await BuildTasksToKnowledgeAsync(lower, upper, cancellationToken);
        var pdiToKnowledge = await BuildPdiToKnowledgeAsync(lower, upper, cancellationToken);
        var knowledgeToIncidents = await BuildKnowledgeToIncidentsAsync(lower, upper, cancellationToken);
        var competencies = await BuildCompetenciesDemandedAsync(lower, upper, cancellationToken);

        return new ReportCrossingsDto(
            From: from,
            To: to,
            IncidentsToTasks: incidentsToTasks,
            TasksToKnowledge: tasksToKnowledge,
            PdiToKnowledge: pdiToKnowledge,
            KnowledgeToIncidents: knowledgeToIncidents,
            CompetenciesDemandedByTasks: competencies);
    }

    // ── §5 Chamados → Tasks ───────────────────────────────────────────────
    // "Quantos problemas (incidentes) geraram Tasks?" — por incidente com
    // ≥1 vínculo em task_incidents, dentro do período de criação do chamado.

    private Task<CrossingStatDto> BuildIncidentsToTasksAsync(
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var incidentIds = Scope(db.Incidents.AsNoTracking(), lower, upper)
            .Select(i => i.Id);
        var links = db.TaskIncidents.AsNoTracking()
            .Where(ti => incidentIds.Contains(ti.IncidentId));

        return StatAsync(links, ti => ti.IncidentId, ti => ti.TaskId, cancellationToken);
    }

    // ── §5 Tasks → Knowledge ──────────────────────────────────────────────
    // "Quantas Tasks concluídas geraram documentação?" — tasks Resolved/Closed
    // no período que têm ≥1 artigo em task_knowledge_articles.

    private Task<CrossingStatDto> BuildTasksToKnowledgeAsync(
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var completedTaskIds = Scope(db.Tasks.AsNoTracking(), lower, upper)
            .Where(t => t.State == TaskState.Closed || t.State == TaskState.Resolved)
            .Select(t => t.Id);
        var links = db.TaskKnowledgeArticles.AsNoTracking()
            .Where(tka => completedTaskIds.Contains(tka.TaskId));

        return StatAsync(links, tka => tka.TaskId, tka => tka.ArticleId, cancellationToken);
    }

    // ── §5 PDI → Knowledge ────────────────────────────────────────────────
    // "Quantos PDIs geraram conhecimento reutilizável?" — artigos criados no
    // período com origem KnowledgeOriginType.Pdi (origem por plano).

    private async Task<CrossingStatDto> BuildPdiToKnowledgeAsync(
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var articleIds = Scope(db.KnowledgeArticles.AsNoTracking(), lower, upper)
            .Select(a => a.Id);
        var links = db.KnowledgeArticleOrigins.AsNoTracking()
            .Where(o => o.OriginType == KnowledgeOriginType.Pdi
                        && articleIds.Contains(o.ArticleId));

        // Origem = plano (OriginId); alvo = artigo.
        return await StatAsync(links, o => o.OriginId, o => o.ArticleId, cancellationToken);
    }

    // ── §5 Knowledge → Chamados ───────────────────────────────────────────
    // "Quantos chamados foram resolvidos utilizando Knowledge?" — artigos no
    // período ligados a incidentes Resolved via knowledge_article_incidents.

    private async Task<CrossingStatDto> BuildKnowledgeToIncidentsAsync(
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var articleIds = Scope(db.KnowledgeArticles.AsNoTracking(), lower, upper)
            .Select(a => a.Id);
        var resolvedIncidentIds = db.Incidents.AsNoTracking()
            .Where(i => i.Status == IncidentStatus.Resolved)
            .Select(i => i.Id);
        var links = db.KnowledgeArticleIncidents.AsNoTracking()
            .Where(ki => articleIds.Contains(ki.ArticleId)
                         && resolvedIncidentIds.Contains(ki.IncidentId));

        return await StatAsync(links, ki => ki.ArticleId, ki => ki.IncidentId, cancellationToken);
    }

    // ── §5 Task → PDI (competências exigidas) ─────────────────────────────
    // "Quais competências estão sendo exigidas pelas Tasks?" — task_pdis
    // liga task → usuário; o objetivo do PDI desse usuário traz a
    // competência. Conta tasks distintas por competência.

    private async Task<IReadOnlyList<CompetencyDemandDto>> BuildCompetenciesDemandedAsync(
        DateTime? lower,
        DateTime? upper,
        CancellationToken cancellationToken)
    {
        var taskIds = Scope(db.Tasks.AsNoTracking(), lower, upper)
            .Select(t => t.Id);

        var demands = await (
            from tp in db.TaskPdis.AsNoTracking()
                .Where(x => taskIds.Contains(x.TaskId))
            join o in db.PdiObjectives.AsNoTracking()
                on tp.UserId equals o.UserId
            where o.CompetencyId != null
            group new { tp.TaskId, o.CompetencyId } by o.CompetencyId!.Value into g
            select new
            {
                CompetencyId = g.Key,
                Tasks = g.Select(x => x.TaskId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);

        if (demands.Count == 0)
            return Array.Empty<CompetencyDemandDto>();

        var competencyIds = demands.Select(d => d.CompetencyId).ToList();
        var names = await db.PdiCompetencies.AsNoTracking()
            .Where(c => competencyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return demands
            .OrderByDescending(d => d.Tasks)
            .ThenBy(d => d.CompetencyId)
            .Select(d => new CompetencyDemandDto(
                d.CompetencyId,
                names.GetValueOrDefault(d.CompetencyId, string.Empty),
                d.Tasks))
            .ToList();
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static async Task<CrossingStatDto> StatAsync<TLink>(
        IQueryable<TLink> links,
        Expression<Func<TLink, Guid>> sourceSelector,
        Expression<Func<TLink, Guid>> targetSelector,
        CancellationToken cancellationToken)
    {
        var sources = await links.Select(sourceSelector).Distinct().CountAsync(cancellationToken);
        var targets = await links.Select(targetSelector).Distinct().CountAsync(cancellationToken);
        var total = await links.CountAsync(cancellationToken);
        return new CrossingStatDto(sources, targets, total);
    }

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
}
