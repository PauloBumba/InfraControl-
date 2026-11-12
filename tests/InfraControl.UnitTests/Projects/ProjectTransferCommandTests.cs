using FluentAssertions;
using InfraControl.Application.Projects.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// Transferência de liderança (F50): o Admin muda o responsável do
/// projeto sem passar pelo form completo de edição — comando dedicado,
/// policy Administer, tag "projects" invalidada.
/// </summary>
public sealed class ProjectTransferCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectTransferCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static TransferProjectCommandHandler Handler(AppDbContext context) =>
        new(
            new ProjectRepository(context),
            NullLogger<TransferProjectCommandHandler>.Instance);

    private static async Task<Project> SeedProjectAsync(
        AppDbContext context, Guid leaderId, ProjectStatus status = ProjectStatus.Draft)
    {
        var project = Project.Create(
            "PRJ-T01", "Projeto de transferência", "Descrição do projeto", leaderId);
        if (status != ProjectStatus.Draft)
        {
            project.TransitionTo(ProjectStatus.Planned);
            project.TransitionTo(ProjectStatus.InProgress);
            if (status == ProjectStatus.Cancelled)
                project.Cancel("Cancelado para o teste.");
            else if (status == ProjectStatus.Completed)
            {
                project.TransitionTo(ProjectStatus.PendingFinalApproval);
                project.TransitionTo(ProjectStatus.Completed);
            }
        }

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task Transfer_SetsNewLeaderAndPromotesMember()
    {
        await using var context = await CreateContextAsync();
        var oldLeader = Guid.NewGuid();
        var newLeader = Guid.NewGuid();
        var project = await SeedProjectAsync(context, oldLeader);

        var dto = await Handler(context).Handle(
            new TransferProjectCommand(project.Id, newLeader),
            CancellationToken.None);

        dto.LeaderId.Should().Be(newLeader);

        var reloaded = await new ProjectRepository(context)
            .GetByIdAsync(project.Id, CancellationToken.None);
        reloaded!.LeaderId.Should().Be(newLeader);
        reloaded.Members.Should().Contain(member =>
            member.UserId == newLeader && member.Role == ProjectMemberRole.Leader);
    }

    [Fact]
    public async Task Transfer_UnknownProject_ThrowsNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => Handler(context).Handle(
            new TransferProjectCommand(unknownId, Guid.NewGuid()),
            CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Projeto '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Transfer_CancelledProject_ThrowsInvalidOperation()
    {
        await using var context = await CreateContextAsync();
        var project = await SeedProjectAsync(
            context, Guid.NewGuid(), ProjectStatus.Cancelled);

        var act = () => Handler(context).Handle(
            new TransferProjectCommand(project.Id, Guid.NewGuid()),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não é possível transferir*");
    }

    [Fact]
    public void Transfer_CommandRequiresAdministerRole()
    {
        var command = new TransferProjectCommand(Guid.NewGuid(), Guid.NewGuid());

        command.RequiredRoles.Should().Equal(RoleAccess.Administer);
        command.CacheKeysToInvalidate.Should().Contain("projects");
    }
}
