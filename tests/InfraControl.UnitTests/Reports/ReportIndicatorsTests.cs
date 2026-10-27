using FluentAssertions;
using InfraControl.Application.Reports.Indicators.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T001 — indicadores detalhados de Tasks/PDI/Knowledge (docs/33 §2–§4).
/// Testa o <c>ReportIndicatorsRepository</c> (leitura composta) direto no
/// Sqlite em memória, no molde de <c>TransversalReportTests</c>.
/// </summary>
public sealed class ReportIndicatorsTests
{
    [Fact]
    public async Task Indicators_ShouldAggregateTasksPdiAndKnowledge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = User.Create(Guid.NewGuid(), "resp@exemplo.test", "Responsável");
        var owner2 = User.Create(Guid.NewGuid(), "outro@exemplo.test", "Outro");
        context.DomainUsers.AddRange(owner, owner2);
        await context.SaveChangesAsync();

        // ── Tasks ────────────────────────────────────────────────────────
        // Resolved (com responsável, resultado e 1 replanejamento).
        var done = TaskItem.Create(
            "Concluída", TaskKind.Manual, responsibleId: owner.Id);
        done.SetResult("entregue");
        done.ChangeState(TaskState.InProgress);
        done.ChangeState(TaskState.Resolved);
        context.Tasks.Add(done);
        context.TaskHistories.Add(TaskHistory.Create(
            done.Id, TaskHistoryAction.Created, owner.Id));
        context.TaskHistories.Add(TaskHistory.Create(
            done.Id, TaskHistoryAction.Replanned, owner.Id));
        context.TaskHistories.Add(TaskHistory.Create(
            done.Id, TaskHistoryAction.Commented, owner.Id));

        // Blocked com impedimento em aberto.
        var blocked = TaskItem.Create("Bloqueada", TaskKind.Maintenance);
        blocked.ChangeState(TaskState.InProgress);
        blocked.ChangeState(TaskState.Blocked);
        context.Tasks.Add(blocked);
        var taskImp = TaskImpediment.Create(blocked.Id, "sem acesso");
        context.TaskImpediments.Add(taskImp);

        // Open atrasada, alta prioridade, kind Incident, com impedimento resolvido.
        var overdue = TaskItem.Create(
            "Atrasada", TaskKind.Incident,
            priority: TaskPriority.High,
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
            responsibleId: owner2.Id);
        context.Tasks.Add(overdue);
        var resolvedImp = TaskImpediment.Create(overdue.Id, "lento");
        resolvedImp.SetResolved(true);
        context.TaskImpediments.Add(resolvedImp);

        // ── Knowledge ────────────────────────────────────────────────────
        var published = KnowledgeArticle.Create(
            "Publicado", "c", null,
            tags: new[] { "rede", "switch" });
        published.TransitionTo(KnowledgeArticleStatus.InReview);
        published.TransitionTo(KnowledgeArticleStatus.Approved);
        published.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(published);
        // 2 versões → versioned.
        context.KnowledgeArticleVersions.Add(
            KnowledgeArticleVersion.SnapshotOf(published, "motivo", owner.Id));
        published.BumpVersion();
        context.KnowledgeArticleVersions.Add(
            KnowledgeArticleVersion.SnapshotOf(published, "motivo2", owner.Id));
        // Origem Task.
        context.KnowledgeArticleOrigins.Add(KnowledgeArticleOrigin.Create(
            published.Id, KnowledgeOriginType.Task, done.Id));

        var draft = KnowledgeArticle.Create(
            "Rascunho", "c", null, tags: new[] { "rede" });
        context.KnowledgeArticles.Add(draft);

        var obsolete = KnowledgeArticle.Create("Obsoleto", "c", null);
        obsolete.TransitionTo(KnowledgeArticleStatus.InReview);
        obsolete.TransitionTo(KnowledgeArticleStatus.Approved);
        obsolete.TransitionTo(KnowledgeArticleStatus.Published);
        obsolete.TransitionTo(KnowledgeArticleStatus.Obsolete);
        context.KnowledgeArticles.Add(obsolete);

        await context.SaveChangesAsync();

        var report = await new ReportIndicatorsRepository(context)
            .GetIndicatorsAsync(null, null, CancellationToken.None);

        // Tasks (§2).
        report.Tasks.Total.Should().Be(3);
        report.Tasks.ByState.Should().ContainKey(nameof(TaskState.Resolved));
        report.Tasks.ByState.Should().ContainKey(nameof(TaskState.Blocked));
        report.Tasks.CompletionRatePercent.Should().Be(33, "1 de 3 concluída");
        report.Tasks.Blocked.Should().Be(1);
        report.Tasks.Overdue.Should().Be(1, "só a 'Atrasada' tem DueDate no passado e aberta");
        report.Tasks.Replannings.Should().Be(1);
        report.Tasks.Updates.Should().Be(2, "Comented + Replanned (não conta Created)");
        report.Tasks.Impediments.Should().Be(2);
        report.Tasks.ImpedimentsResolved.Should().Be(1);
        report.Tasks.WithResult.Should().Be(1);
        report.Tasks.ByPriority.Should().ContainKey(nameof(TaskPriority.High));
        report.Tasks.ByKind.Should().ContainKey(nameof(TaskKind.Incident));
        report.Tasks.ByResponsible.Should().HaveCount(
            3, "owner, owner2 e a task 'blocked' sem responsável (null)");
        report.Tasks.ByResponsible.Should().Contain(
            r => r.ResponsibleId == owner.Id && r.ResponsibleName == "Responsável");
        report.Tasks.ByResponsible.Should().Contain(r => r.ResponsibleId == null);

        // Knowledge (§4).
        report.Knowledge.Total.Should().Be(3);
        report.Knowledge.Published.Should().Be(1);
        report.Knowledge.Draft.Should().Be(1);
        report.Knowledge.Obsolete.Should().Be(1);
        report.Knowledge.Versioned.Should().Be(1, "só o 'Publicado' tem 2 versões");
        report.Knowledge.ByOrigin.Should().ContainKey(nameof(KnowledgeOriginType.Task));
        report.Knowledge.TopTags.Should().Contain(t => t.Tag == "rede" && t.Count == 2);

        // PDI (§3) — vazio, sem planos.
        report.Pdi.TotalPlans.Should().Be(0);
        report.Pdi.CompletionRatePercent.Should().Be(0);
    }

    [Fact]
    public async Task Indicators_WithPdiPlan_ShouldAggregateObjectivesAndCompetencies()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = User.Create(Guid.NewGuid(), "pdi@exemplo.test", "Dono");
        context.DomainUsers.Add(owner);
        var actor = new PdiChangeActor(owner.Id, "admin@exemplo.test", "pdi@exemplo.test");
        context.IndividualDevelopmentPlans.Add(IndividualDevelopmentPlan.Create(
            owner.Id, "Estratégia", DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PdiStatus.Completed, null, null, null, actor));
        await context.SaveChangesAsync();

        var competency = PdiCompetency.Create("Liderança", null);
        context.PdiCompetencies.Add(competency);
        await context.SaveChangesAsync();

        var objective = PdiObjective.Create(
            owner.Id, "Objetivo", null, competency.Id,
            null, null, null, null, null, 1);
        context.PdiObjectives.Add(objective);
        await context.SaveChangesAsync();

        var strategy = PdiStrategy.Create(objective.Id, "Estratégia", null, 1);
        context.PdiStrategies.Add(strategy);
        await context.SaveChangesAsync();

        var phase = PdiPhase.Create(
            strategy.Id, "Fase", null, null, null, null, null,
            PdiPhaseStatus.InProgress, 50, 1);
        context.PdiPhases.Add(phase);
        await context.SaveChangesAsync();

        context.PdiImpediments.Add(PdiImpediment.Create(
            phase.Id, "tempo", "falta tempo", DateOnly.FromDateTime(DateTime.Today)));
        await context.SaveChangesAsync();

        var report = await new ReportIndicatorsRepository(context)
            .GetIndicatorsAsync(null, null, CancellationToken.None);

        report.Pdi.TotalPlans.Should().Be(1);
        report.Pdi.CompletedPlans.Should().Be(1);
        report.Pdi.CompletionRatePercent.Should().Be(100);
        report.Pdi.ObjectivesTotal.Should().Be(1);
        report.Pdi.CompetenciesWorked.Should().Be(1);
        report.Pdi.TotalImpediments.Should().Be(1);
    }

    [Fact]
    public async Task Indicators_Empty_ShouldReturnZeros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var report = await new ReportIndicatorsRepository(context)
            .GetIndicatorsAsync(null, null, CancellationToken.None);

        report.Tasks.Total.Should().Be(0);
        report.Tasks.CompletionRatePercent.Should().Be(0);
        report.Pdi.TotalPlans.Should().Be(0);
        report.Knowledge.Total.Should().Be(0);
        report.Knowledge.TopTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Indicators_WithPeriodFilter_ShouldExcludeOutsideRange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Tasks.Add(TaskItem.Create("Recente", TaskKind.Manual));
        await context.SaveChangesAsync();

        var all = await new ReportIndicatorsRepository(context)
            .GetIndicatorsAsync(null, null, CancellationToken.None);
        all.Tasks.Total.Should().Be(1, "sem filtro entram todas as tasks");

        var futureFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var filtered = await new ReportIndicatorsRepository(context)
            .GetIndicatorsAsync(futureFrom, null, CancellationToken.None);
        filtered.Tasks.Total.Should().Be(0, "o filtro de período deve excluir");
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
