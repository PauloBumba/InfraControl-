using FluentAssertions;
using InfraControl.Application.Projects;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// F28-T003 — CQRS de fases do Projeto + cálculo de avanço ponderado.
/// </summary>
public sealed class ProjectPhaseCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectPhaseCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static ProjectRepository Projects(AppDbContext context) => new(context);
    private static ProjectPhaseRepository Phases(AppDbContext context) => new(context);

    private static AddProjectPhaseCommandHandler AddHandler(AppDbContext context) =>
        new(Projects(context), Phases(context), NullLogger<AddProjectPhaseCommandHandler>.Instance);

    private static UpdateProjectPhaseCommandHandler UpdateHandler(AppDbContext context) =>
        new(Phases(context), NullLogger<UpdateProjectPhaseCommandHandler>.Instance);

    private static ReorderProjectPhasesCommandHandler ReorderHandler(AppDbContext context) =>
        new(Projects(context), Phases(context), NullLogger<ReorderProjectPhasesCommandHandler>.Instance);

    private static SetPhaseStatusCommandHandler StatusHandler(AppDbContext context) =>
        new(Phases(context), NullLogger<SetPhaseStatusCommandHandler>.Instance);

    private static GetProjectPhasesQueryHandler QueryHandler(AppDbContext context) =>
        new(Projects(context), Phases(context));

    private static async Task<Guid> SeedProjectAsync(AppDbContext context)
    {
        var project = Project.Create(
            "PRJ-100",
            "Expansão Wi-Fi",
            "Cobertura do campus novo",
            Guid.NewGuid());
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueryRead()
    {
        new AddProjectPhaseCommand(Guid.NewGuid(), "x").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateProjectPhaseCommand(Guid.NewGuid(), "x").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new ReorderProjectPhasesCommand(Guid.NewGuid(), [Guid.NewGuid()]).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new SetPhaseStatusCommand(Guid.NewGuid(), "Start").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetProjectPhasesQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Progress calculator ───────────────────────────────────────────

    [Fact]
    public void ProgressCalculator_Empty_ReturnsZero()
    {
        ProjectProgressCalculator.ComputeOverallPercent([]).Should().Be(0);
    }

    [Fact]
    public void ProgressCalculator_AverageAwayFromZero()
    {
        var projectId = Guid.NewGuid();
        var a = ProjectPhase.Create(projectId, "A", 1);
        a.SetProgress(50);
        var b = ProjectPhase.Create(projectId, "B", 2);
        b.SetProgress(100);

        // (50+100)/2 = 75
        ProjectProgressCalculator.ComputeOverallPercent([a, b]).Should().Be(75);
    }

    [Fact]
    public void ProgressCalculator_RoundsAwayFromZero()
    {
        var projectId = Guid.NewGuid();
        var a = ProjectPhase.Create(projectId, "A", 1);
        a.SetProgress(33);
        var b = ProjectPhase.Create(projectId, "B", 2);
        b.SetProgress(34);

        // (33+34)/2 = 33.5 → 34
        ProjectProgressCalculator.ComputeOverallPercent([a, b]).Should().Be(34);
    }

    // ── Add ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_UnknownProject_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context).Handle(
            new AddProjectPhaseCommand(unknown, "Levantamento"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Projeto '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Add_FirstAndSecond_ShouldAssignSequentialOrder()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var first = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(
                projectId, "Levantamento",
                CompletionCriteria: "Desenho aprovado"),
            CancellationToken.None);

        var second = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Implantação"),
            CancellationToken.None);

        first.Order.Should().Be(1);
        first.Status.Should().Be(nameof(ProjectPhaseStatus.NotStarted));
        first.CompletionCriteria.Should().Be("Desenho aprovado");
        second.Order.Should().Be(2);
    }

    // ── Update ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldChangeMetadataAndPercent()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Fase"),
            CancellationToken.None);

        var updated = await UpdateHandler(context).Handle(
            new UpdateProjectPhaseCommand(
                created.Id,
                "Fase revisada",
                Description: "Escopo ampliado",
                CompletionCriteria: "Checklist OK",
                Percent: 25),
            CancellationToken.None);

        updated.Title.Should().Be("Fase revisada");
        updated.Description.Should().Be("Escopo ampliado");
        updated.CompletionCriteria.Should().Be("Checklist OK");
        updated.Percent.Should().Be(25);
        updated.Status.Should().Be(nameof(ProjectPhaseStatus.InProgress));
    }

    // ── Reorder ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_ShouldRewriteOrdersOneBased()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var a = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "A"), CancellationToken.None);
        var b = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "B"), CancellationToken.None);
        var c = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "C"), CancellationToken.None);

        await ReorderHandler(context).Handle(
            new ReorderProjectPhasesCommand(projectId, [c.Id, a.Id, b.Id]),
            CancellationToken.None);

        var summary = await QueryHandler(context).Handle(
            new GetProjectPhasesQuery(projectId), CancellationToken.None);

        summary.Phases.Select(p => p.Title).Should().Equal("C", "A", "B");
        summary.Phases.Select(p => p.Order).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Reorder_IncompleteList_ShouldFail()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var a = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "A"), CancellationToken.None);
        await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "B"), CancellationToken.None);

        var action = () => ReorderHandler(context).Handle(
            new ReorderProjectPhasesCommand(projectId, [a.Id]),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exatamente todas as fases*");
    }

    // ── Set status ────────────────────────────────────────────────────

    [Fact]
    public async Task SetStatus_Validate_ShouldCompleteAndAutoStartNext()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var first = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Primeira"), CancellationToken.None);
        var second = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Segunda"), CancellationToken.None);

        await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(first.Id, "Start"), CancellationToken.None);
        await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(first.Id, "Submit"), CancellationToken.None);
        await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(first.Id, "Validate"), CancellationToken.None);

        var summary = await QueryHandler(context).Handle(
            new GetProjectPhasesQuery(projectId), CancellationToken.None);

        var phase1 = summary.Phases.Single(p => p.Id == first.Id);
        var phase2 = summary.Phases.Single(p => p.Id == second.Id);

        phase1.Status.Should().Be(nameof(ProjectPhaseStatus.Completed));
        phase2.Status.Should().Be(nameof(ProjectPhaseStatus.InProgress),
            "validada → próxima fase começa automaticamente");
        summary.OverallProgressPercent.Should().Be(50);
    }

    [Fact]
    public async Task SetStatus_Start_WhenPreviousIncomplete_ShouldFail()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Primeira"), CancellationToken.None);
        var second = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Segunda"), CancellationToken.None);

        var action = () => StatusHandler(context).Handle(
            new SetPhaseStatusCommand(second.Id, "Start"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ainda não foi concluída*");
    }

    [Fact]
    public async Task SetStatus_Reject_RequiresReason()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var phase = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Fase"), CancellationToken.None);

        await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(phase.Id, "Start"), CancellationToken.None);
        await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(phase.Id, "Submit"), CancellationToken.None);

        var noReason = () => StatusHandler(context).Handle(
            new SetPhaseStatusCommand(phase.Id, "Reject"), CancellationToken.None);
        await noReason.Should().ThrowAsync<ArgumentException>().WithMessage("*motivo*");

        var rejected = await StatusHandler(context).Handle(
            new SetPhaseStatusCommand(phase.Id, "Reject", "Checklist incompleto"),
            CancellationToken.None);

        rejected.Status.Should().Be(nameof(ProjectPhaseStatus.InProgress));
    }

    [Fact]
    public async Task SetStatus_InvalidAction_ShouldFailPtBr()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var phase = await AddHandler(context).Handle(
            new AddProjectPhaseCommand(projectId, "Fase"), CancellationToken.None);

        var action = () => StatusHandler(context).Handle(
            new SetPhaseStatusCommand(phase.Id, "Pause"), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Start, Submit, Validate ou Reject*");
    }

    // ── Query ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPhases_UnknownProject_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetProjectPhasesQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Projeto '{unknown}' não encontrado.");
    }
}
