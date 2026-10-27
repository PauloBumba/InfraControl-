using FluentAssertions;
using InfraControl.Application.Reports.Dashboards.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T003 — dashboards por papel (docs/33 §6–§8). Testa o
/// <c>ReportDashboardsRepository</c> no Sqlite em memória.
/// </summary>
public sealed class ReportDashboardsTests
{
    [Fact]
    public async Task Colaborador_ShouldScopeToUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var me = User.Create(Guid.NewGuid(), "eu@exemplo.test", "Eu");
        var other = User.Create(Guid.NewGuid(), "outro@exemplo.test", "Outro");
        context.DomainUsers.AddRange(me, other);
        await context.SaveChangesAsync();

        // Tasks: uma concluída e uma em andamento minhas; uma de outro usuário.
        var done = TaskItem.Create("Minha concluída", TaskKind.Manual, responsibleId: me.Id);
        done.ChangeState(TaskState.InProgress);
        done.ChangeState(TaskState.Resolved);
        context.Tasks.Add(done);
        var inProgress = TaskItem.Create("Minha em andamento", TaskKind.Manual, responsibleId: me.Id);
        inProgress.ChangeState(TaskState.InProgress);
        context.Tasks.Add(inProgress);
        context.Tasks.Add(TaskItem.Create("De outro", TaskKind.Manual, responsibleId: other.Id));
        await context.SaveChangesAsync();

        // PDI meu com 1 fase.
        context.IndividualDevelopmentPlans.Add(IndividualDevelopmentPlan.Create(
            me.Id, "Estratégia", DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PdiStatus.InProgress, null, null, null,
            new PdiChangeActor(me.Id, "admin@exemplo.test", "eu@exemplo.test")));
        await context.SaveChangesAsync();
        var objective = PdiObjective.Create(me.Id, "Obj", null, null, null, null, null, null, null, 1);
        context.PdiObjectives.Add(objective);
        await context.SaveChangesAsync();
        var strategy = PdiStrategy.Create(objective.Id, "Est", null, 1);
        context.PdiStrategies.Add(strategy);
        await context.SaveChangesAsync();
        context.PdiPhases.Add(PdiPhase.Create(
            strategy.Id, "Fase", null, null, null, null, null,
            PdiPhaseStatus.InProgress, 40, 1));
        await context.SaveChangesAsync();

        // Knowledge meu publicado.
        var article = KnowledgeArticle.Create("Meu artigo", "c", null, authorId: me.Id);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        var report = await new ReportDashboardsRepository(context)
            .GetColaboradorAsync(me.Id, CancellationToken.None);

        report.MyTasks.InProgress.Should().Be(1);
        report.MyTasks.Completed.Should().Be(1);
        report.MyTasks.Pending.Should().Be(1, "só a em andamento conta como pendente");
        report.MyPdi.HasPlan.Should().BeTrue();
        report.MyPdi.Phases.Should().Be(1);
        report.MyPdi.ProgressPercent.Should().Be(40);
        report.MyKnowledge.Published.Should().Be(1);
    }

    [Fact]
    public async Task Gestor_ShouldAggregateOrgWide()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "g@exemplo.test", "Gestor");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var blocked = TaskItem.Create("Bloqueada", TaskKind.Incident);
        blocked.ChangeState(TaskState.InProgress);
        blocked.ChangeState(TaskState.Blocked);
        context.Tasks.Add(blocked);
        var improvement = TaskItem.Create("Preventiva", TaskKind.Preventive);
        context.Tasks.Add(improvement);
        await context.SaveChangesAsync();

        context.IndividualDevelopmentPlans.Add(IndividualDevelopmentPlan.Create(
            user.Id, "Est", DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PdiStatus.Completed, null, null, null,
            new PdiChangeActor(user.Id, "admin@exemplo.test", "g@exemplo.test")));
        await context.SaveChangesAsync();

        var article = KnowledgeArticle.Create("Publicado", "c", null);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(article);
        var review = KnowledgeArticle.Create("Em revisão", "c", null);
        review.TransitionTo(KnowledgeArticleStatus.InReview);
        context.KnowledgeArticles.Add(review);
        await context.SaveChangesAsync();

        var report = await new ReportDashboardsRepository(context)
            .GetGestorAsync(CancellationToken.None);

        report.Tasks.Blocked.Should().Be(1);
        report.Tasks.Improvements.Should().Be(1);
        report.Pdi.Completed.Should().Be(1);
        report.Knowledge.Published.Should().Be(1);
        report.Knowledge.InReview.Should().Be(1);
        report.KnowledgePendingApproval.Should().Be(1);
    }

    [Fact]
    public async Task Administrativo_ShouldFilterByResponsibleAndKind()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var userA = User.Create(Guid.NewGuid(), "a@exemplo.test", "Alice");
        var userB = User.Create(Guid.NewGuid(), "b@exemplo.test", "Bruno");
        context.DomainUsers.AddRange(userA, userB);
        await context.SaveChangesAsync();

        context.Tasks.Add(TaskItem.Create(
            "Incidente A", TaskKind.Incident, responsibleId: userA.Id));
        context.Tasks.Add(TaskItem.Create(
            "Manual B", TaskKind.Manual, responsibleId: userB.Id));
        await context.SaveChangesAsync();

        var byResponsible = await new ReportDashboardsRepository(context)
            .GetAdministrativoAsync(
                null, null, userA.Id, null, CancellationToken.None);
        byResponsible.Tasks.Total.Should().Be(1, "filtrei por responsável A");
        byResponsible.ByResponsible.Should().HaveCount(1);
        byResponsible.ByResponsible[0].ResponsibleName.Should().Be("Alice");

        var byKind = await new ReportDashboardsRepository(context)
            .GetAdministrativoAsync(
                null, null, null, TaskKindFilter.Incident, CancellationToken.None);
        byKind.Tasks.Total.Should().Be(1, "filtrei por tipo Incident");
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
