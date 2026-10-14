using FluentAssertions;
using InfraControl.Application.Reports.Transversal.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// ADR-049 — relatório transversal: contagens por status nos 5 agregados +
/// tempo médio de resolução de incidente, com filtro de período. Testa o
/// <c>ReportRepository</c> (read-model composto) direto no Sqlite em memória.
/// </summary>
public sealed class TransversalReportTests
{
    [Fact]
    public async Task Snapshot_ShouldAggregateCountsAcrossModules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var owner = User.Create(
            Guid.NewGuid(), "lider@exemplo.test", "Líder");
        context.DomainUsers.Add(owner);
        var owner2 = User.Create(
            Guid.NewGuid(), "colaborador@exemplo.test", "Colaborador");
        context.DomainUsers.Add(owner2);
        await context.SaveChangesAsync();

        var actor = new PdiChangeActor(
            owner.Id, "admin@exemplo.test", "lider@exemplo.test");

        // PDI: 1 Completed + 1 Active → total 2, concluídos 1 (1 plano por usuário).
        context.IndividualDevelopmentPlans.Add(IndividualDevelopmentPlan.Create(
            owner.Id, "Estratégia A", DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PdiStatus.Completed, null, null, null, actor));
        context.IndividualDevelopmentPlans.Add(IndividualDevelopmentPlan.Create(
            owner2.Id, "Estratégia B", DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
            PdiStatus.Active, null, null, null, actor));

        // Tasks: 1 Resolved (via Open→InProgress→Resolved) + 1 Open atrasada.
        var doneTask = TaskItem.Create("Task concluída", TaskKind.Manual);
        doneTask.ChangeState(TaskState.InProgress);
        doneTask.ChangeState(TaskState.Resolved);
        context.Tasks.Add(doneTask);
        context.Tasks.Add(TaskItem.Create(
            "Task atrasada", TaskKind.Manual,
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-3))));

        // Knowledge: 2 rascunhos → total 2, publicados 0.
        context.KnowledgeArticles.Add(KnowledgeArticle.Create(
            "Artigo 1", "conteudo", categoryId: null));
        context.KnowledgeArticles.Add(KnowledgeArticle.Create(
            "Artigo 2", "conteudo", categoryId: null));

        // Projetos: 1 Draft + 1 Planned → total 2.
        context.Projects.Add(Project.Create(
            "PRJ-001", "Projeto Um", "desc", leaderId: owner.Id,
            plannedStartDate: DateOnly.FromDateTime(DateTime.Today)));
        var project2 = Project.Create(
            "PRJ-002", "Projeto Dois", "desc", leaderId: owner.Id);
        project2.TransitionTo(ProjectStatus.Planned);
        context.Projects.Add(project2);

        // Incidentes: 1 Resolved + 1 Open → total 2, resolvido 1.
        var resolved = Incident.Create("Incidente resolvido", IncidentSeverity.Medium);
        resolved.ChangeStatus(IncidentStatus.Resolved);
        context.Incidents.Add(resolved);
        context.Incidents.Add(Incident.Create("Incidente aberto", IncidentSeverity.Low));

        await context.SaveChangesAsync();

        var report = await new ReportRepository(context)
            .GetSnapshotAsync(null, null, CancellationToken.None);

        report.Pdi.Total.Should().Be(2);
        report.Pdi.Completed.Should().Be(1);
        report.Pdi.ByStatus.Should().ContainKey(nameof(PdiStatus.Completed));

        report.Tasks.Total.Should().Be(2);
        report.Tasks.Completed.Should().Be(1);
        report.Tasks.Overdue.Should().Be(1);

        report.Knowledge.Total.Should().Be(2);
        report.Knowledge.Published.Should().Be(0);
        report.Knowledge.Draft.Should().Be(2);

        report.Projects.Total.Should().Be(2);
        report.Projects.ByStatus.Should().ContainKey(nameof(ProjectStatus.Planned));

        report.Incidents.Total.Should().Be(2);
        report.Incidents.Resolved.Should().Be(1);
        report.Incidents.AverageResolutionHours.Should().NotBeNull(
            "há 1 incidente resolvido, a média deve ser calculada");
    }

    [Fact]
    public async Task Snapshot_WithPeriodFilter_ShouldExcludeOutsideRange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Tasks.Add(TaskItem.Create("Recente", TaskKind.Manual));
        await context.SaveChangesAsync();

        var all = await new ReportRepository(context)
            .GetSnapshotAsync(null, null, CancellationToken.None);
        all.Tasks.Total.Should().Be(1, "sem filtro entram todas as tasks");

        // Uma data inicial no futuro (UTC, como o CreateDateTime gravado) exclui tudo.
        var futureFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var filtered = await new ReportRepository(context)
            .GetSnapshotAsync(futureFrom, null, CancellationToken.None);
        filtered.Tasks.Total.Should().Be(0, "o filtro de período deve excluir o intervalo");
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
