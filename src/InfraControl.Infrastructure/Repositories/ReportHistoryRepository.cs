using InfraControl.Application.Reports.History;
using InfraControl.Application.Reports.History.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F25-T005 §13 — série histórica mensal (docs/33 §13). Agregação por mês de
/// <c>CreateDateTime</c> no banco (EF traduz <c>Year</c>/<c>Month</c> por
/// provider), sem tabela de snapshot. "Concluídas" = tasks criadas no mês que
/// estão concluídas hoje (domínio sem carimbo de conclusão).
/// </summary>
public sealed class ReportHistoryRepository(AppDbContext db) : IReportHistoryRepository
{
    public async Task<ReportHistoryDto> GetHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var lower = from?.ToDateTime(TimeOnly.MinValue);
        var upper = to?.ToDateTime(TimeOnly.MaxValue);

        var tasks = await GroupByMonthAsync(
            db.Tasks.AsNoTracking(), lower, upper, null, cancellationToken);
        var completedTasks = await GroupByMonthAsync(
            db.Tasks.AsNoTracking(), lower, upper,
            t => t.State == TaskState.Resolved || t.State == TaskState.Closed,
            cancellationToken);
        var knowledge = await GroupByMonthAsync(
            db.KnowledgeArticles.AsNoTracking(), lower, upper, null, cancellationToken);
        var pdis = await GroupByMonthAsync(
            db.IndividualDevelopmentPlans.AsNoTracking(), lower, upper, null, cancellationToken);

        var keys = tasks.Keys
            .Concat(completedTasks.Keys)
            .Concat(knowledge.Keys)
            .Concat(pdis.Keys)
            .Distinct()
            .ToList();

        if (from is not null && to is not null)
            keys = AllMonths(from.Value, to.Value);

        var months = keys
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => new HistoryMonthDto(
                k,
                tasks.GetValueOrDefault(k),
                completedTasks.GetValueOrDefault(k),
                knowledge.GetValueOrDefault(k),
                pdis.GetValueOrDefault(k)))
            .ToList();

        return new ReportHistoryDto(from, to, months);
    }

    private static async Task<Dictionary<string, int>> GroupByMonthAsync<T>(
        IQueryable<T> query,
        DateTime? lower,
        DateTime? upper,
        System.Linq.Expressions.Expression<Func<T, bool>>? extra,
        CancellationToken cancellationToken)
        where T : Domain.Common.Entities.Entity
    {
        if (lower is not null)
            query = query.Where(e => e.CreateDateTime >= lower.Value);
        if (upper is not null)
            query = query.Where(e => e.CreateDateTime <= upper.Value);
        if (extra is not null)
            query = query.Where(extra);

        var rows = await query
            .GroupBy(e => new { e.CreateDateTime.Year, e.CreateDateTime.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => $"{r.Year:D4}-{r.Month:D2}",
            r => r.Count);
    }

    private static List<string> AllMonths(DateOnly from, DateOnly to)
    {
        var result = new List<string>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var end = new DateOnly(to.Year, to.Month, 1);
        while (cursor <= end)
        {
            result.Add($"{cursor.Year:D4}-{cursor.Month:D2}");
            cursor = cursor.AddMonths(1);
        }

        return result;
    }
}
