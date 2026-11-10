using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Projects;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.Application.Projects.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// Spec evolução 2026-11-07 §5/§6 — visibilidade por usuário e fluxo formal
/// de conclusão/aprovação de projetos.
/// </summary>
public sealed class ProjectConclusionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectConclusionCommandTests()
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
    private static ProjectConclusionRepository Conclusions(AppDbContext context) => new(context);
    private static ProjectHistoryRepository History(AppDbContext context) => new(context);

    private static SubmitProjectConclusionCommandHandler SubmitHandler(
        AppDbContext context, ICurrentUserContext user) =>
        new(Projects(context), Conclusions(context), History(context), user,
            NullLogger<SubmitProjectConclusionCommandHandler>.Instance);

    private static ApproveProjectConclusionCommandHandler ApproveHandler(
        AppDbContext context, ICurrentUserContext admin) =>
        new(Projects(context), Conclusions(context), History(context), admin,
            new FakeSender(), NullLogger<ApproveProjectConclusionCommandHandler>.Instance);

    private static ReturnProjectConclusionCommandHandler ReturnHandler(
        AppDbContext context, ICurrentUserContext admin) =>
        new(Projects(context), Conclusions(context), History(context), admin,
            NullLogger<ReturnProjectConclusionCommandHandler>.Instance);

    private static async Task<(Guid ProjectId, Guid LeaderId)> SeedInProgressProjectAsync(
        AppDbContext context, Guid? leaderId = null)
    {
        var leader = leaderId ?? Guid.NewGuid();
        var project = Project.Create(
            "PRJ-500", "Projeto X", "Descrição", leader);
        project.TransitionTo(ProjectStatus.Planned);
        project.TransitionTo(ProjectStatus.InProgress);
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return (project.Id, leader);
    }

    private static SubmitProjectConclusionCommandHandler LeaderSubmitHandler(
        AppDbContext context, Guid leaderId) =>
        SubmitHandler(context, new FakeCurrentUser(leaderId, UserRole.Operator.ToString()));

    private static SubmitProjectConclusionCommand ValidSubmit(Guid projectId) =>
        new(projectId,
            "Relatório final entregue",
            "Configuração e testes executados",
            "Sistema no ar com 99% de uptime",
            "Indisponibilidade do fornecedor",
            "Contorno via link redundante",
            "Relatório em anexo",
            DateTimeOffset.UtcNow);

    // ── Submit ────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_ShouldCreatePendingConclusionAndMoveToApproval()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);

        var dto = await LeaderSubmitHandler(context, leaderId).Handle(
            ValidSubmit(projectId), CancellationToken.None);

        dto.Status.Should().Be(nameof(ProjectConclusionStatus.Pending));

        var project = await context.Projects.SingleAsync(p => p.Id == projectId);
        project.Status.Should().Be(ProjectStatus.PendingFinalApproval);

        var conclusion = await context.ProjectConclusions.SingleAsync();
        conclusion.Status.Should().Be(ProjectConclusionStatus.Pending);

        (await context.ProjectHistoryEntries.CountAsync(
            h => h.Type == ProjectHistoryType.ConclusionSubmitted)).Should().Be(1);
    }

    [Fact]
    public async Task Submit_WhenNotInProgress_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var leaderId = Guid.NewGuid();
        var project = Project.Create("PRJ-501", "Rascunho", "Desc", leaderId);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var act = () => LeaderSubmitHandler(context, leaderId).Handle(
            ValidSubmit(project.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*em andamento*");
    }

    [Fact]
    public async Task Submit_WhenPendingExists_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);
        var handler = LeaderSubmitHandler(context, leaderId);

        await handler.Handle(ValidSubmit(projectId), CancellationToken.None);

        // A pendência move o projeto para PendingFinalApproval — a segunda
        // submissão é bloqueada (status ou pendência existente).
        var act = () => handler.Handle(ValidSubmit(projectId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Approve ───────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_ShouldCompleteProjectAndApproveConclusion()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);
        var adminId = Guid.NewGuid();

        await LeaderSubmitHandler(context, leaderId).Handle(ValidSubmit(projectId), CancellationToken.None);

        var dto = await ApproveHandler(
                context, new FakeCurrentUser(adminId, UserRole.Admin.ToString()))
            .Handle(new ApproveProjectConclusionCommand(projectId, "Aprovado."),
                CancellationToken.None);

        dto.Status.Should().Be(nameof(ProjectStatus.Completed));

        var conclusion = await context.ProjectConclusions.SingleAsync();
        conclusion.Status.Should().Be(ProjectConclusionStatus.Approved);
        conclusion.DecidedById.Should().Be(adminId);

        (await context.ProjectHistoryEntries.CountAsync(
            h => h.Type == ProjectHistoryType.ConclusionApproved)).Should().Be(1);
    }

    [Fact]
    public async Task Approve_WithoutPendingConclusion_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);

        var act = () => ApproveHandler(
                context, new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString()))
            .Handle(new ApproveProjectConclusionCommand(projectId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pendente*");
    }

    // ── Return (reject / request changes) ─────────────────────────────

    [Fact]
    public async Task Reject_ShouldReturnProjectToInProgress()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);
        var admin = new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString());

        await LeaderSubmitHandler(context, leaderId).Handle(ValidSubmit(projectId), CancellationToken.None);

        var dto = await ReturnHandler(context, admin).Handle(
            new ReturnProjectConclusionCommand(projectId, "reject", "Faltam evidências."),
            CancellationToken.None);

        dto.Status.Should().Be(nameof(ProjectStatus.InProgress));

        var conclusion = await context.ProjectConclusions.SingleAsync();
        conclusion.Status.Should().Be(ProjectConclusionStatus.Rejected);
        conclusion.DecisionNotes.Should().Be("Faltam evidências.");
    }

    [Fact]
    public async Task RequestChanges_ShouldReturnProjectToInProgress()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);
        var admin = new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString());

        await LeaderSubmitHandler(context, leaderId).Handle(ValidSubmit(projectId), CancellationToken.None);

        var dto = await ReturnHandler(context, admin).Handle(
            new ReturnProjectConclusionCommand(projectId, "requestchanges", "Complementar resultados."),
            CancellationToken.None);

        dto.Status.Should().Be(nameof(ProjectStatus.InProgress));

        var conclusion = await context.ProjectConclusions.SingleAsync();
        conclusion.Status.Should().Be(ProjectConclusionStatus.ChangesRequested);
    }

    [Fact]
    public async Task Reject_WithoutJustification_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);
        var admin = new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString());

        await LeaderSubmitHandler(context, leaderId).Handle(ValidSubmit(projectId), CancellationToken.None);

        var act = () => ReturnHandler(context, admin).Handle(
            new ReturnProjectConclusionCommand(projectId, "reject", "  "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Visibilidade (spec §5) ────────────────────────────────────────

    [Fact]
    public async Task Search_NonAdmin_SeesOnlyAssignedProjects()
    {
        await using var context = await CreateContextAsync();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var mine = Project.Create("PRJ-600", "Meu projeto", "Desc", memberId);
        var foreign = Project.Create("PRJ-601", "Projeto alheio", "Desc", otherId);
        context.Projects.AddRange(mine, foreign);
        await context.SaveChangesAsync();

        var handler = new SearchProjectsQueryHandler(
            Projects(context),
            new FakeCurrentUser(memberId, UserRole.Viewer.ToString()));

        var result = await handler.Handle(
            new SearchProjectsQuery(Page: 1, PageSize: 20), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(p => p.Id == mine.Id);
    }

    [Fact]
    public async Task Search_Admin_SeesAllProjects()
    {
        await using var context = await CreateContextAsync();
        context.Projects.AddRange(
            Project.Create("PRJ-610", "A", "Desc", Guid.NewGuid()),
            Project.Create("PRJ-611", "B", "Desc", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = new SearchProjectsQueryHandler(
            Projects(context),
            new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString()));

        var result = await handler.Handle(
            new SearchProjectsQuery(Page: 1, PageSize: 20), CancellationToken.None);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProject_NonMember_ShouldThrowNotFound()
    {
        await using var context = await CreateContextAsync();
        var (projectId, leaderId) = await SeedInProgressProjectAsync(context);

        var handler = new GetProjectQueryHandler(
            Projects(context),
            new FakeCurrentUser(Guid.NewGuid(), UserRole.Viewer.ToString()));

        var act = () => handler.Handle(
            new GetProjectQuery(projectId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Projeto '{projectId}' não encontrado.");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private sealed class FakeCurrentUser(Guid userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId => userId;
        public string? UserName => "user@example.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private sealed class FakeSender : ISender
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResponse)!);

        public Task<TResponse> Send<TRequest, TResponse>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            => Task.FromResult(default(TResponse)!);

        public Task<object?> Send(
            IRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public Task<object?> Send(
            object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public Task Send<TRequest>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(
            object request, CancellationToken cancellationToken = default)
            => Empty<object?>();

        private static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
