using FluentAssertions;
using InfraControl.Application.Projects;
using InfraControl.Application.Projects.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// F28-T005 — governança e homologação pelo Administrador.
/// </summary>
public sealed class ProjectGovernanceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectGovernanceCommandTests()
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

    private static SubmitProjectForApprovalCommandHandler SubmitHandler(AppDbContext context) =>
        new(Projects(context), NullLogger<SubmitProjectForApprovalCommandHandler>.Instance);

    private static ApproveProjectCommandHandler ApproveHandler(
        AppDbContext context,
        ICurrentUserContext currentUser) =>
        new(Projects(context), currentUser, NullLogger<ApproveProjectCommandHandler>.Instance);

    private static async Task<Guid> SeedProjectAsync(AppDbContext context)
    {
        var project = Project.Create(
            "PRJ-200",
            "Modernização de Core",
            "Migração dos switches de borda para SDN",
            Guid.NewGuid());

        project.TransitionTo(ProjectStatus.Planned);
        project.TransitionTo(ProjectStatus.InProgress);

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUserContext
    {
        public Guid? UserId => userId;
        public string? UserName => "admin@example.com";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    [Fact]
    public void Commands_ShouldRequireExpectedRoles()
    {
        new SubmitProjectForApprovalCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

        new ApproveProjectCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Administer);
    }

    [Fact]
    public async Task Submit_ShouldTransitionToPendingFinalApproval()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var result = await SubmitHandler(context).Handle(
            new SubmitProjectForApprovalCommand(projectId),
            CancellationToken.None);

        result.Status.Should().Be(ProjectStatus.PendingFinalApproval.ToString());

        var project = await context.Projects.SingleAsync(p => p.Id == projectId);
        project.Status.Should().Be(ProjectStatus.PendingFinalApproval);
    }

    [Fact]
    public async Task Approve_ShouldCompleteProjectAndCaptureAdminDecision()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var adminId = Guid.NewGuid();

        await SubmitHandler(context).Handle(
            new SubmitProjectForApprovalCommand(projectId),
            CancellationToken.None);

        var result = await ApproveHandler(context, new FakeCurrentUser(adminId, UserRole.Admin.ToString()))
            .Handle(
                new ApproveProjectCommand(projectId, "Projeto homologado com sucesso."),
                CancellationToken.None);

        result.Status.Should().Be(ProjectStatus.Completed.ToString());
        result.AdminApproverId.Should().Be(adminId);
        result.ApprovalNotes.Should().Be("Projeto homologado com sucesso.");

        var project = await context.Projects.SingleAsync(p => p.Id == projectId);
        project.Status.Should().Be(ProjectStatus.Completed);
        project.AdminApproverId.Should().Be(adminId);
        project.ApprovalNotes.Should().Be("Projeto homologado com sucesso.");
        project.ApprovedAt.Should().NotBeNull();
    }
}
