using FluentAssertions;
using InfraControl.Application.Reports.History.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T005 §13 — série histórica mensal (docs/33 §13). Testa o
/// <c>ReportHistoryRepository</c> no Sqlite em memória.
/// </summary>
public sealed class ReportHistoryTests
{
    [Fact]
    public async Task History_ShouldGroupByMonth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        // 2 tasks e 1 knowledge em 2026-01; 1 task concluída em 2026-02.
        var jan = new DateTime(2026, 1, 10);
        await AddAtAsync(context, jan, () => TaskItem.Create("T1", TaskKind.Manual));
        await AddAtAsync(context, jan.AddDays(10), () => TaskItem.Create("T2", TaskKind.Manual));
        await AddAtAsync(context, jan.AddDays(5), () => KnowledgeArticle.Create("K1", "c", null));

        var done = TaskItem.Create("Concluída", TaskKind.Manual);
        done.ChangeState(TaskState.InProgress);
        done.ChangeState(TaskState.Resolved);
        await AddAtAsync(context, new DateTime(2026, 2, 5), () => done);

        var report = await new ReportHistoryRepository(context)
            .GetHistoryAsync(null, null, CancellationToken.None);

        var janRow = report.Months.Single(m => m.Month == "2026-01");
        janRow.Tasks.Should().Be(2);
        janRow.Knowledge.Should().Be(1);
        janRow.CompletedTasks.Should().Be(0);

        var febRow = report.Months.Single(m => m.Month == "2026-02");
        febRow.Tasks.Should().Be(1);
        febRow.CompletedTasks.Should().Be(1, "a task de fev está concluída");

        report.Months.Should().BeInAscendingOrder(m => m.Month);
    }

    [Fact]
    public async Task History_Empty_ShouldReturnNoMonths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var report = await new ReportHistoryRepository(context)
            .GetHistoryAsync(null, null, CancellationToken.None);

        report.Months.Should().BeEmpty();
    }

    [Fact]
    public async Task History_WithRange_ShouldFillMissingMonths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await AddAtAsync(
            context, new DateTime(2026, 1, 10),
            () => TaskItem.Create("T1", TaskKind.Manual));

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 3, 1);
        var report = await new ReportHistoryRepository(context)
            .GetHistoryAsync(from, to, CancellationToken.None);

        report.Months.Should().HaveCount(3, "jan/fev/mar preenchidos");
        report.Months.Single(m => m.Month == "2026-02").Tasks.Should().Be(0);
        report.Months.Single(m => m.Month == "2026-03").Tasks.Should().Be(0);
    }

    /// <summary>
    /// ApplyAuditFields sobrescreve CreateDateTime na 1ª passada (Added);
    /// gravar numa 2ª passada (Modified) preserva a data determinística.
    /// </summary>
    private static async Task AddAtAsync(
        AppDbContext context,
        DateTime createdAt,
        Func<object> factory)
    {
        var entity = (Domain.Common.Entities.Entity)factory();
        context.Add(entity);
        await context.SaveChangesAsync();
        entity.CreateDateTime = createdAt;
        await context.SaveChangesAsync();
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
