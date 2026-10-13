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
/// CRUD-Projetos (2026-10-12) — sub-agregados que antes eram só leitura:
/// Impedimentos (CRUD + Resolve/Reopen + query) e Membros (add/upsert papel +
/// remove), via repositórios reais em SQLite in-memory.
/// </summary>
public sealed class ProjectImpedimentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectImpedimentCommandTests()
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
    private static ProjectImpedimentRepository Impediments(AppDbContext context) => new(context);

    private static CreateProjectImpedimentCommandHandler CreateHandler(AppDbContext context) =>
        new(Projects(context), Impediments(context), NullLogger<CreateProjectImpedimentCommandHandler>.Instance);

    private static UpdateProjectImpedimentCommandHandler UpdateHandler(AppDbContext context) =>
        new(Impediments(context), NullLogger<UpdateProjectImpedimentCommandHandler>.Instance);

    private static DeleteProjectImpedimentCommandHandler DeleteHandler(AppDbContext context) =>
        new(Impediments(context), NullLogger<DeleteProjectImpedimentCommandHandler>.Instance);

    private static ResolveProjectImpedimentCommandHandler ResolveHandler(AppDbContext context) =>
        new(Impediments(context), NullLogger<ResolveProjectImpedimentCommandHandler>.Instance);

    private static ReopenProjectImpedimentCommandHandler ReopenHandler(AppDbContext context) =>
        new(Impediments(context), NullLogger<ReopenProjectImpedimentCommandHandler>.Instance);

    private static GetProjectImpedimentsQueryHandler QueryHandler(AppDbContext context) =>
        new(Impediments(context));

    private static AddProjectMemberCommandHandler AddMemberHandler(AppDbContext context) =>
        new(Projects(context), NullLogger<AddProjectMemberCommandHandler>.Instance);

    private static RemoveProjectMemberCommandHandler RemoveMemberHandler(AppDbContext context) =>
        new(Projects(context), NullLogger<RemoveProjectMemberCommandHandler>.Instance);

    private static async Task<Guid> SeedProjectAsync(
        AppDbContext context, Guid? leaderId = null, string code = "PRJ-900")
    {
        var project = Project.Create(
            code,
            "Projeto de teste",
            "Descrição",
            leaderId ?? Guid.NewGuid());
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueryRead()
    {
        new CreateProjectImpedimentCommand(Guid.NewGuid(), "t", "d", "Technical", "Low").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateProjectImpedimentCommand(Guid.NewGuid(), "t", "d", "Technical", "Low").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteProjectImpedimentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new ResolveProjectImpedimentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new ReopenProjectImpedimentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new AddProjectMemberCommand(Guid.NewGuid(), Guid.NewGuid(), "Member").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new RemoveProjectMemberCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetProjectImpedimentsQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Impedimentos ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistImpediment()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var dto = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(
                projectId, "Fornecedor atrasou", "Equipamento em trânsito",
                "Vendor", "High", "Escalonar com o fornecedor"),
            CancellationToken.None);

        dto.ProjectId.Should().Be(projectId);
        dto.Title.Should().Be("Fornecedor atrasou");
        dto.Category.Should().Be("Vendor");
        dto.Severity.Should().Be("High");
        dto.MitigationPlan.Should().Be("Escalonar com o fornecedor");
        dto.Status.Should().Be("Open");
        dto.IsResolved.Should().BeFalse();

        var persisted = await Impediments(context).ListByProjectIdAsync(projectId);
        persisted.Should().ContainSingle(i => i.Id == dto.Id);
    }

    [Fact]
    public async Task Create_OnMissingProject_ShouldThrow()
    {
        await using var context = await CreateContextAsync();

        var act = () => CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(
                Guid.NewGuid(), "t", "d", "Technical", "Low"),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("Bogus", "Low")]
    [InlineData("Technical", "Bogus")]
    public async Task Create_WithInvalidEnum_ShouldThrow(string category, string severity)
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var act = () => CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t", "d", category, severity),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Update_ShouldChangeFields()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t1", "d1", "Technical", "Low"),
            CancellationToken.None);

        var updated = await UpdateHandler(context).Handle(
            new UpdateProjectImpedimentCommand(
                created.Id, "t2", "d2", "Budget", "Critical", "mitiga"),
            CancellationToken.None);

        updated.Title.Should().Be("t2");
        updated.Category.Should().Be("Budget");
        updated.Severity.Should().Be("Critical");
        updated.MitigationPlan.Should().Be("mitiga");
    }

    [Fact]
    public async Task Delete_ShouldRemoveImpediment()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t", "d", "Technical", "Low"),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteProjectImpedimentCommand(created.Id), CancellationToken.None);

        var remaining = await Impediments(context).ListByProjectIdAsync(projectId);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_ShouldMarkResolved()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t", "d", "Technical", "Low"),
            CancellationToken.None);

        var resolved = await ResolveHandler(context).Handle(
            new ResolveProjectImpedimentCommand(created.Id), CancellationToken.None);

        resolved.IsResolved.Should().BeTrue();
        resolved.Status.Should().Be("Resolved");
        resolved.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_AfterResolved_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t", "d", "Technical", "Low"),
            CancellationToken.None);
        await ResolveHandler(context).Handle(
            new ResolveProjectImpedimentCommand(created.Id), CancellationToken.None);

        var act = () => UpdateHandler(context).Handle(
            new UpdateProjectImpedimentCommand(created.Id, "t2", "d2", "Technical", "Low"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Reopen_ShouldReturnToOpen()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "t", "d", "Technical", "Low"),
            CancellationToken.None);
        await ResolveHandler(context).Handle(
            new ResolveProjectImpedimentCommand(created.Id), CancellationToken.None);

        var reopened = await ReopenHandler(context).Handle(
            new ReopenProjectImpedimentCommand(created.Id), CancellationToken.None);

        reopened.IsResolved.Should().BeFalse();
        reopened.Status.Should().Be("Open");
        reopened.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Query_ShouldListImpedimentsOfProject()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var otherProjectId = await SeedProjectAsync(context, code: "PRJ-901");
        await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(projectId, "um", "d", "Technical", "Low"),
            CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateProjectImpedimentCommand(otherProjectId, "outro", "d", "Technical", "Low"),
            CancellationToken.None);

        var result = await QueryHandler(context).Handle(
            new GetProjectImpedimentsQuery(projectId), CancellationToken.None);

        result.Should().ContainSingle(i => i.Title == "um");
    }

    // ── Membros ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddMember_ShouldAddToProject()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var userId = Guid.NewGuid();

        await AddMemberHandler(context).Handle(
            new AddProjectMemberCommand(projectId, userId, "Member"), CancellationToken.None);

        var project = await Projects(context).GetByIdAsync(projectId);
        project!.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == ProjectMemberRole.Member);
    }

    [Fact]
    public async Task AddMember_Twice_ShouldUpsertRole()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var userId = Guid.NewGuid();

        await AddMemberHandler(context).Handle(
            new AddProjectMemberCommand(projectId, userId, "Member"), CancellationToken.None);
        await AddMemberHandler(context).Handle(
            new AddProjectMemberCommand(projectId, userId, "Reviewer"), CancellationToken.None);

        var project = await Projects(context).GetByIdAsync(projectId);
        var members = project!.Members.Where(m => m.UserId == userId).ToList();
        members.Should().ContainSingle();
        members[0].Role.Should().Be(ProjectMemberRole.Reviewer);
    }

    [Fact]
    public async Task RemoveMember_ShouldRemoveFromProject()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var userId = Guid.NewGuid();
        await AddMemberHandler(context).Handle(
            new AddProjectMemberCommand(projectId, userId, "Member"), CancellationToken.None);

        await RemoveMemberHandler(context).Handle(
            new RemoveProjectMemberCommand(projectId, userId), CancellationToken.None);

        var project = await Projects(context).GetByIdAsync(projectId);
        project!.Members.Should().NotContain(m => m.UserId == userId);
    }

    [Fact]
    public async Task AddMember_WithInvalidRole_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var act = () => AddMemberHandler(context).Handle(
            new AddProjectMemberCommand(projectId, Guid.NewGuid(), "Bogus"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
