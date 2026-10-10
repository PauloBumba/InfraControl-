using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

public sealed class ProjectCrudCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectCrudCommandTests()
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

    private static ProjectRepository Repository(AppDbContext context) => new(context);

    private static CreateProjectCommandHandler CreateHandler(AppDbContext context, Guid creatorId) =>
        new(Repository(context), new FakeCurrentUser(creatorId), NullLogger<CreateProjectCommandHandler>.Instance);

    private static UpdateProjectCommandHandler UpdateHandler(AppDbContext context) =>
        new(Repository(context), NullLogger<UpdateProjectCommandHandler>.Instance);

    private static SearchProjectsQueryHandler SearchHandler(AppDbContext context) =>
        new(Repository(context));

    [Fact]
    public async Task Create_ShouldGenerateNextCodeAndPersistProject()
    {
        await using var context = await CreateContextAsync();
        var creatorId = Guid.NewGuid();

        var dto = await CreateHandler(context, creatorId).Handle(
            new CreateProjectCommand(
                Title: "Nova plataforma de monitoramento",
                Description: "Estrutura de coleta de métricas e alertas",
                LeaderId: Guid.NewGuid(),
                Priority: "High"),
            CancellationToken.None);

        dto.Code.Should().Be("PRJ-001");
        dto.Title.Should().Be("Nova plataforma de monitoramento");
        dto.Status.Should().Be(nameof(ProjectStatus.Draft));
        dto.CreatedById.Should().Be(creatorId);

        (await context.Projects.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Search_ShouldHonorStatusAndPriorityFilters()
    {
        await using var context = await CreateContextAsync();
        var leaderId = Guid.NewGuid();

        var active = Project.Create(
            "PRJ-001",
            "Projeto ativo",
            "Descrição ativa",
            leaderId,
            ProjectPriority.High);
        active.TransitionTo(ProjectStatus.Planned);
        active.TransitionTo(ProjectStatus.InProgress);

        var draft = Project.Create(
            "PRJ-002",
            "Projeto rascunho",
            "Descrição rascunho",
            Guid.NewGuid(),
            ProjectPriority.Low);

        context.Projects.AddRange(active, draft);
        await context.SaveChangesAsync();

        var result = await SearchHandler(context).Handle(
            new SearchProjectsQuery(
                Status: nameof(ProjectStatus.InProgress),
                Priority: nameof(ProjectPriority.High),
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.Id == active.Id);
    }

    [Fact]
    public async Task Update_ShouldRefreshProjectMetadata()
    {
        await using var context = await CreateContextAsync();
        var leaderId = Guid.NewGuid();
        var project = Project.Create(
            "PRJ-100",
            "Projeto original",
            "Descrição original",
            leaderId,
            ProjectPriority.Medium);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var updated = await UpdateHandler(context).Handle(
            new UpdateProjectCommand(
                Id: project.Id,
                Title: "Projeto revisado",
                Description: "Descrição revisada",
                Justification: "Necessidade de mudança de escopo",
                Priority: "Critical",
                LeaderId: leaderId,
                ManagerId: Guid.NewGuid()),
            CancellationToken.None);

        updated.Title.Should().Be("Projeto revisado");
        updated.Description.Should().Be("Descrição revisada");
        updated.Priority.Should().Be(nameof(ProjectPriority.Critical));
        updated.Status.Should().Be(nameof(ProjectStatus.Draft));
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId => userId;
        public string? UserName => "creator@example.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => [];
    }
}
