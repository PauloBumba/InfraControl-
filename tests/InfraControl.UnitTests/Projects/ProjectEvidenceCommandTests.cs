using System.Text;
using FluentAssertions;
using InfraControl.Application.Projects;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.Application.Projects.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// CRUD-Projetos (2026-10-12) — evidências técnicas (ADR-043 · ADR-033/034):
/// upload em disco local (store real com dir temporário, sem Docker),
/// revisão única, download de stream e exclusão arquivo+registro.
/// </summary>
public sealed class ProjectEvidenceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _storageDir;

    public ProjectEvidenceCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _storageDir = Path.Combine(
            Path.GetTempPath(),
            "infracontrol-project-evidence-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            if (Directory.Exists(_storageDir))
                Directory.Delete(_storageDir, recursive: true);
        }
        catch (IOException)
        {
            // Limpeza best-effort do temp — não afeta as asserções.
        }
    }

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static ProjectRepository Projects(AppDbContext context) => new(context);
    private static ProjectEvidenceRepository Evidence(AppDbContext context) => new(context);

    private IProjectEvidenceFileStore Store() =>
        new PhysicalProjectEvidenceFileStore(
            Options.Create(new ProjectEvidenceOptions { StoragePath = _storageDir }));

    private static FakeCurrentUser User() => new();

    private static UploadProjectEvidenceCommandHandler UploadHandler(
        AppDbContext context, IProjectEvidenceFileStore store, ICurrentUserContext user)
        => new(Projects(context), Evidence(context), store, user,
            NullLogger<UploadProjectEvidenceCommandHandler>.Instance);

    private static ReviewProjectEvidenceCommandHandler ReviewHandler(
        AppDbContext context, ICurrentUserContext user)
        => new(Evidence(context), user,
            NullLogger<ReviewProjectEvidenceCommandHandler>.Instance);

    private static DeleteProjectEvidenceCommandHandler DeleteHandler(
        AppDbContext context, IProjectEvidenceFileStore store)
        => new(Evidence(context), store,
            NullLogger<DeleteProjectEvidenceCommandHandler>.Instance);

    private static GetProjectEvidenceQueryHandler ListHandler(AppDbContext context)
        => new(Evidence(context));

    private static DownloadProjectEvidenceQueryHandler DownloadHandler(
        AppDbContext context, IProjectEvidenceFileStore store)
        => new(Evidence(context), store);

    private static async Task<Guid> SeedProjectAsync(AppDbContext context, string code = "PRJ-800")
    {
        var project = Project.Create(code, "Projeto", "Descrição", Guid.NewGuid());
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    private static UploadProjectEvidenceCommand Upload(
        Guid projectId, string fileName = "relatorio.pdf", long? fileSize = null,
        byte[]? content = null)
    {
        content ??= Encoding.UTF8.GetBytes("conteudo-da-evidencia");
        return new UploadProjectEvidenceCommand(
            projectId,
            ProjectEvidenceType.TestReport,
            "Descrição da evidência",
            fileName,
            "application/pdf",
            fileSize ?? content.Length,
            new MemoryStream(content));
    }

    private static async Task<ProjectEvidenceDto> SeedEvidenceAsync(
        AppDbContext context, IProjectEvidenceFileStore store, Guid projectId)
        => await UploadHandler(context, store, User()).Handle(
            Upload(projectId), CancellationToken.None);

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueriesTheirRoles()
    {
        Upload(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new ReviewProjectEvidenceCommand(
            Guid.NewGuid(), ProjectEvidenceReviewDecision.Validated).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteProjectEvidenceCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetProjectEvidenceQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
        new DownloadProjectEvidenceQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    // ── Upload ────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ShouldPersistRowAndFile()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var store = Store();

        var dto = await UploadHandler(context, store, User()).Handle(
            Upload(projectId), CancellationToken.None);

        dto.ProjectId.Should().Be(projectId);
        dto.Type.Should().Be("TestReport");
        dto.Status.Should().Be("Pending");
        dto.FileName.Should().Be("relatorio.pdf");
        dto.ContentType.Should().Be("application/pdf");

        // Row persistida + binário gravado no disco temporário.
        (await Evidence(context).ListByProjectIdAsync(projectId))
            .Should().ContainSingle(e => e.Id == dto.Id);
        await using var stream = await store.OpenReadAsync(dto.Id, ".pdf");
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be("conteudo-da-evidencia");
    }

    [Fact]
    public async Task Upload_OnMissingProject_ShouldThrow()
    {
        await using var context = await CreateContextAsync();

        var act = () => UploadHandler(context, Store(), User()).Handle(
            Upload(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Upload_WithInvalidType_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var act = () => UploadHandler(context, Store(), User()).Handle(
            new UploadProjectEvidenceCommand(
                projectId, (ProjectEvidenceType)99, null, "a.pdf", "application/pdf", 1,
                new MemoryStream([1])),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Upload_EmptyFile_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var act = () => UploadHandler(context, Store(), User()).Handle(
            Upload(projectId, fileSize: 0), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Upload_OversizedFile_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);

        var act = () => UploadHandler(context, Store(), User()).Handle(
            Upload(projectId, fileSize: ProjectEvidence.MaxFileSize + 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Revisão ───────────────────────────────────────────────────────

    [Fact]
    public async Task Review_Validated_ShouldMarkValidated()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var store = Store();
        var created = await SeedEvidenceAsync(context, store, projectId);

        var reviewed = await ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, ProjectEvidenceReviewDecision.Validated, null),
            CancellationToken.None);

        reviewed.Status.Should().Be("Validated");
        reviewed.ReviewedAt.Should().NotBeNull();
        reviewed.RejectionReason.Should().BeNull();
    }

    [Fact]
    public async Task Review_Rejected_ShouldStoreReason()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await SeedEvidenceAsync(context, Store(), projectId);

        var reviewed = await ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, ProjectEvidenceReviewDecision.Rejected, "Documento ilegível"),
            CancellationToken.None);

        reviewed.Status.Should().Be("Rejected");
        reviewed.RejectionReason.Should().Be("Documento ilegível");
    }

    [Fact]
    public async Task Review_RejectedWithoutReason_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await SeedEvidenceAsync(context, Store(), projectId);

        var act = () => ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, ProjectEvidenceReviewDecision.Rejected, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Review_Twice_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await SeedEvidenceAsync(context, Store(), projectId);
        await ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, ProjectEvidenceReviewDecision.Validated, null),
            CancellationToken.None);

        var act = () => ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, ProjectEvidenceReviewDecision.Rejected, "motivo"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Review_WithInvalidDecision_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var created = await SeedEvidenceAsync(context, Store(), projectId);

        var act = () => ReviewHandler(context, User()).Handle(
            new ReviewProjectEvidenceCommand(
                created.Id, (ProjectEvidenceReviewDecision)99, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Download / listagem / exclusão ────────────────────────────────

    [Fact]
    public async Task Download_ShouldReturnStoredContent()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var store = Store();
        var created = await SeedEvidenceAsync(context, store, projectId);

        var download = await DownloadHandler(context, store).Handle(
            new DownloadProjectEvidenceQuery(created.Id), CancellationToken.None);

        download.FileName.Should().Be("relatorio.pdf");
        download.ContentType.Should().Be("application/pdf");
        using var reader = new StreamReader(download.Content);
        (await reader.ReadToEndAsync()).Should().Be("conteudo-da-evidencia");
    }

    [Fact]
    public async Task Download_MissingEvidence_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var store = Store();

        var act = () => DownloadHandler(context, store).Handle(
            new DownloadProjectEvidenceQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task List_ShouldFilterByProject()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context, "PRJ-810");
        var otherProjectId = await SeedProjectAsync(context, "PRJ-811");
        var store = Store();
        await SeedEvidenceAsync(context, store, projectId);
        await SeedEvidenceAsync(context, store, otherProjectId);

        var result = await ListHandler(context).Handle(
            new GetProjectEvidenceQuery(projectId), CancellationToken.None);

        result.Should().ContainSingle(e => e.ProjectId == projectId);
    }

    [Fact]
    public async Task Delete_ShouldRemoveRowAndFile()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var store = Store();
        var created = await SeedEvidenceAsync(context, store, projectId);

        await DeleteHandler(context, store).Handle(
            new DeleteProjectEvidenceCommand(created.Id), CancellationToken.None);

        (await Evidence(context).ListByProjectIdAsync(projectId)).Should().BeEmpty();
        var act = () => store.OpenReadAsync(created.Id, ".pdf");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? UserName => "autor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => RoleAccess.Operate;
    }
}
