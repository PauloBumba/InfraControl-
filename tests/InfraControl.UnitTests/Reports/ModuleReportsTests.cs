using FluentAssertions;
using InfraControl.Application.Reports.Modules.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T004 — relatórios estruturados por módulo (docs/33 §9–§12). Testa o
/// <c>ModuleReportsRepository</c> no Sqlite em memória.
/// </summary>
public sealed class ModuleReportsTests
{
    [Fact]
    public async Task TasksReport_ShouldSummarizeAndAggregate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "r@exemplo.test", "Resp");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var done = TaskItem.Create("Concluída", TaskKind.Manual, responsibleId: user.Id);
        done.SetResult("entregue");
        done.ChangeState(TaskState.InProgress);
        done.ChangeState(TaskState.Resolved);
        context.Tasks.Add(done);
        context.TaskHistories.Add(TaskHistory.Create(
            done.Id, TaskHistoryAction.Replanned, user.Id));

        var overdue = TaskItem.Create(
            "Atrasada", TaskKind.Manual,
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        context.Tasks.Add(overdue);

        var blocked = TaskItem.Create("Bloqueada", TaskKind.Manual);
        blocked.ChangeState(TaskState.InProgress);
        blocked.ChangeState(TaskState.Blocked);
        context.Tasks.Add(blocked);
        await context.SaveChangesAsync();

        var report = await new ModuleReportsRepository(context)
            .GetTasksReportAsync(null, null, CancellationToken.None);

        report.Summary.Quantity.Should().Be(3);
        report.Summary.Completed.Should().Be(1);
        report.Summary.Overdue.Should().Be(1);
        report.Summary.Blocked.Should().Be(1);
        report.Execution.Replannings.Should().Be(1);
        report.Execution.WithResult.Should().Be(1);
        report.Execution.Responsibles.Should().Contain(r => r.Name == "Resp");
    }

    [Fact]
    public async Task KnowledgeReport_ShouldCountUsageAndVersions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "k@exemplo.test", "Autor");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var article = KnowledgeArticle.Create("Artigo", "c", null, authorId: user.Id);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        context.KnowledgeArticleVersions.Add(
            KnowledgeArticleVersion.SnapshotOf(article, "motivo", user.Id));
        var task = TaskItem.Create("Usa o artigo", TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        context.TaskKnowledgeArticles.Add(
            TaskKnowledgeArticle.Create(task.Id, article.Id));
        await context.SaveChangesAsync();

        var report = await new ModuleReportsRepository(context)
            .GetKnowledgeReportAsync(null, null, CancellationToken.None);

        report.Created.Should().Be(1);
        report.Published.Should().Be(1);
        report.Versions.Should().Be(1);
        report.UsageLinks.Should().Be(1);
        report.MostUsed.Should().HaveCount(1);
        report.MostUsed[0].Uses.Should().Be(1);
        report.MostUsed[0].Title.Should().Be("Artigo");
    }

    [Fact]
    public async Task GerencialReport_ShouldComposeAndFlagAttention()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var overdue = TaskItem.Create(
            "Atrasada", TaskKind.Manual,
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        context.Tasks.Add(overdue);
        await context.SaveChangesAsync();
        context.TaskImpediments.Add(TaskImpediment.Create(overdue.Id, "sem acesso"));
        await context.SaveChangesAsync();

        var report = await new ModuleReportsRepository(context)
            .GetGerencialReportAsync(null, null, CancellationToken.None);

        report.Tasks.Overdue.Should().Be(1);
        report.PrincipaisImpedimentos.Should().Contain("sem acesso");
        report.PontosAtencao.Should().Contain(a => a.Contains("atrasada"));
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
