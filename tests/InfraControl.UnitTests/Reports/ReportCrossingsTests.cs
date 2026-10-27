using FluentAssertions;
using InfraControl.Application.Reports.Crossings.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T002 — cruzamentos integrados (docs/33 §5). Testa o
/// <c>ReportCrossingsRepository</c> no Sqlite em memória, no molde de
/// <c>ReportIndicatorsTests</c>.
/// </summary>
public sealed class ReportCrossingsTests
{
    [Fact]
    public async Task Crossings_ShouldLinkModules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = User.Create(Guid.NewGuid(), "resp@exemplo.test", "Responsável");
        context.DomainUsers.Add(owner);
        await context.SaveChangesAsync();

        // ── PDI (para §5 Task→PDI e origem Pdi→Knowledge) ────────────────
        var plan = IndividualDevelopmentPlan.Create(
            owner.Id, "Estratégia", DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PdiStatus.InProgress, null, null, null,
            new PdiChangeActor(owner.Id, "admin@exemplo.test", "resp@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        var competency = PdiCompetency.Create("Liderança", null);
        context.PdiCompetencies.Add(competency);
        await context.SaveChangesAsync();
        context.PdiObjectives.Add(PdiObjective.Create(
            owner.Id, "Objetivo", null, competency.Id,
            null, null, null, null, null, 1));
        await context.SaveChangesAsync();

        // ── Chamado resolvido ────────────────────────────────────────────
        var incident = Incident.Create("Chamado recorrente", IncidentSeverity.Medium);
        incident.ChangeStatus(IncidentStatus.Resolved);
        context.Incidents.Add(incident);

        // ── Tasks ────────────────────────────────────────────────────────
        // Concluída → gera documentação e está ligada ao PDI.
        var done = TaskItem.Create("Concluída", TaskKind.Manual);
        done.ChangeState(TaskState.InProgress);
        done.ChangeState(TaskState.Resolved);
        context.Tasks.Add(done);
        // Aberta → gerada pelo chamado.
        var open = TaskItem.Create("Do chamado", TaskKind.Incident);
        context.Tasks.Add(open);
        await context.SaveChangesAsync();

        // ── Knowledge publicado ──────────────────────────────────────────
        var article = KnowledgeArticle.Create("Artigo", "conteudo", null);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        // ── Vínculos N:N ─────────────────────────────────────────────────
        // Chamado → Task (incidente gerou task).
        context.TaskIncidents.Add(TaskIncident.Create(open.Id, incident.Id));
        // Task concluída → Knowledge (gerou documentação).
        context.TaskKnowledgeArticles.Add(
            TaskKnowledgeArticle.Create(done.Id, article.Id));
        // PDI → Knowledge (artigo tem origem no plano).
        context.KnowledgeArticleOrigins.Add(
            KnowledgeArticleOrigin.Create(
                article.Id, KnowledgeOriginType.Pdi, plan.Id));
        // Knowledge → Chamado (artigo usado para resolver o chamado).
        context.KnowledgeArticleIncidents.Add(
            KnowledgeArticleIncident.Create(article.Id, incident.Id));
        // Task → PDI (task ligada ao usuário dono do plano).
        context.TaskPdis.Add(TaskPdi.Create(done.Id, owner.Id));
        await context.SaveChangesAsync();

        var report = await new ReportCrossingsRepository(context)
            .GetCrossingsAsync(null, null, CancellationToken.None);

        report.IncidentsToTasks.Sources.Should().Be(1, "1 chamado gerou task");
        report.IncidentsToTasks.Targets.Should().Be(1);
        report.IncidentsToTasks.Links.Should().Be(1);

        report.TasksToKnowledge.Sources.Should().Be(1, "1 task concluída gerou doc");
        report.TasksToKnowledge.Targets.Should().Be(1);
        report.TasksToKnowledge.Links.Should().Be(1);

        report.PdiToKnowledge.Sources.Should().Be(1, "1 plano gerou artigo");
        report.PdiToKnowledge.Targets.Should().Be(1);
        report.PdiToKnowledge.Links.Should().Be(1);

        report.KnowledgeToIncidents.Sources.Should().Be(1, "1 artigo resolving 1 chamado");
        report.KnowledgeToIncidents.Targets.Should().Be(1);
        report.KnowledgeToIncidents.Links.Should().Be(1);

        report.CompetenciesDemandedByTasks.Should().HaveCount(1);
        report.CompetenciesDemandedByTasks[0].Name.Should().Be("Liderança");
        report.CompetenciesDemandedByTasks[0].Tasks.Should().Be(1);
    }

    [Fact]
    public async Task Crossings_Empty_ShouldReturnZeros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var report = await new ReportCrossingsRepository(context)
            .GetCrossingsAsync(null, null, CancellationToken.None);

        report.IncidentsToTasks.Links.Should().Be(0);
        report.TasksToKnowledge.Links.Should().Be(0);
        report.PdiToKnowledge.Links.Should().Be(0);
        report.KnowledgeToIncidents.Links.Should().Be(0);
        report.CompetenciesDemandedByTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Crossings_KnowledgeToIncidents_ShouldIgnoreUnresolved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var openIncident = Incident.Create("Em aberto", IncidentSeverity.Low);
        context.Incidents.Add(openIncident);
        var article = KnowledgeArticle.Create("Artigo", "c", null);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        context.KnowledgeArticleIncidents.Add(
            KnowledgeArticleIncident.Create(article.Id, openIncident.Id));
        await context.SaveChangesAsync();

        var report = await new ReportCrossingsRepository(context)
            .GetCrossingsAsync(null, null, CancellationToken.None);

        report.KnowledgeToIncidents.Links.Should().Be(
            0, "chamado não resolvido não conta em Knowledge→Chamados");
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
