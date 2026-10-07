using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Ports;
using InfraControl.Application.Tasks.Queries;
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

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4c — evidências (§11/ADR-033): upload em disco local com
/// allowlist/10 MB, download (arquivo ausente = 404 pt-BR), exclusão
/// arquivo+registro e histórico EvidenceAdded/Removed ("nome (kB)").
/// O store usa um diretório temporário por teste — sem Docker.
/// </summary>
public sealed class TaskEvidenceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _storageDir;

    public TaskEvidenceCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _storageDir = Path.Combine(
            Path.GetTempPath(),
            "infracontrol-evidence-" + Guid.NewGuid().ToString("N"));
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

    private static TaskItemRepository Repo(AppDbContext context) => new(context);
    private static TaskEvidenceRepository EvidenceRepo(AppDbContext context) => new(context);

    private IEvidenceFileStore Store() =>
        new LocalEvidenceFileStore(
            Options.Create(new StorageOptions { EvidencePath = _storageDir }),
            NullLogger<LocalEvidenceFileStore>.Instance);

    private static UploadTaskEvidenceCommandHandler UploadHandler(
        AppDbContext context, IEvidenceFileStore store, RecordingTaskHistoryWriter history)
        => new(
            Repo(context),
            EvidenceRepo(context),
            store,
            history,
            NullLogger<UploadTaskEvidenceCommandHandler>.Instance);

    private static DeleteTaskEvidenceCommandHandler DeleteHandler(
        AppDbContext context, IEvidenceFileStore store, RecordingTaskHistoryWriter history)
        => new(
            EvidenceRepo(context),
            store,
            history,
            NullLogger<DeleteTaskEvidenceCommandHandler>.Instance);

    private static GetTaskEvidenceQueryHandler QueryHandler(AppDbContext context)
        => new(Repo(context), EvidenceRepo(context));

    private static GetTaskEvidenceDownloadQueryHandler DownloadHandler(
        AppDbContext context, IEvidenceFileStore store)
        => new(EvidenceRepo(context), store);

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task para evidências", TaskKind.Incident);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static byte[] Bytes(string payload) =>
        System.Text.Encoding.UTF8.GetBytes(payload);

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueriesRead()
    {
        new UploadTaskEvidenceCommand(Guid.NewGuid(), "a.png", null, [1])
            .RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteTaskEvidenceCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetTaskEvidenceQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
        new GetTaskEvidenceDownloadQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Upload ────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UploadHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(
                new UploadTaskEvidenceCommand(unknown, "a.png", "image/png", Bytes("x")),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Upload_ShouldPersistRowFileAndHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var content = Bytes("conteúdo da evidência");

        var dto = await UploadHandler(context, Store(), history).Handle(
            new UploadTaskEvidenceCommand(
                taskId, "resultado.png", "image/png", content, "medido em campo"),
            CancellationToken.None);

        dto.TaskId.Should().Be(taskId);
        dto.FileName.Should().Be("resultado.png");
        dto.ContentType.Should().Be("image/png");
        dto.FileSize.Should().Be(content.Length);
        dto.Note.Should().Be("medido em campo");
        dto.UploadedAt.Should().NotBe(default);

        var row = await context.TaskEvidences.AsNoTracking()
            .SingleAsync(evidence => evidence.Id == dto.Id);

        // Arquivo no disco com o nome gerado (GUID + extensão), bytes íntegros.
        var path = Path.Combine(_storageDir, row.StoredName);
        File.Exists(path).Should().BeTrue();
        (await File.ReadAllBytesAsync(path)).Should().BeEquivalentTo(content);

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.EvidenceAdded);
        history.Records[0].Detail.Should().Be("resultado.png (1 kB)");
    }

    [Fact]
    public async Task Upload_DisallowedExtension_ShouldThrowAndStoreNothing()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => UploadHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(
                new UploadTaskEvidenceCommand(taskId, "virus.exe", "application/x-msdownload", Bytes("x")),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Tipo de arquivo não permitido*");

        (await context.TaskEvidences.AsNoTracking().CountAsync())
            .Should().Be(0);
        Directory.Exists(_storageDir).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_EmptyFile_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => UploadHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(
                new UploadTaskEvidenceCommand(taskId, "vazio.txt", "text/plain", []),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O arquivo está vazio.*");
    }

    [Fact]
    public async Task Upload_LongFileName_ShouldTruncateHistoryDetail()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var fileName = new string('l', 194) + ".txt"; // 199 chars ≤ 200

        await UploadHandler(context, Store(), history).Handle(
            new UploadTaskEvidenceCommand(taskId, fileName, "text/plain", Bytes("x")),
            CancellationToken.None);

        var detail = history.Records.Single().Detail;
        detail!.Length.Should().BeLessThanOrEqualTo(TaskHistory.DetailMaxLength);
        detail.Should().Contain("…").And.EndWith("(1 kB)");
    }

    // ── Download ──────────────────────────────────────────────────────

    [Fact]
    public async Task Download_ShouldReturnOriginalMetadataAndBytes()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var content = Bytes("arquivo de evidência completo");

        var uploaded = await UploadHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(
                new UploadTaskEvidenceCommand(taskId, "anexo.pdf", "application/pdf", content),
                CancellationToken.None);

        var download = await DownloadHandler(context, Store()).Handle(
            new GetTaskEvidenceDownloadQuery(uploaded.Id), CancellationToken.None);

        download.FileName.Should().Be("anexo.pdf");
        download.ContentType.Should().Be("application/pdf");
        download.Content.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task Download_UnknownEvidence_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DownloadHandler(context, Store()).Handle(
            new GetTaskEvidenceDownloadQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Evidência '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Download_FileMissingOnDisk_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var uploaded = await UploadHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(
                new UploadTaskEvidenceCommand(taskId, "sumiu.txt", "text/plain", Bytes("x")),
                CancellationToken.None);

        var storedName = (await context.TaskEvidences.AsNoTracking()
            .SingleAsync(evidence => evidence.Id == uploaded.Id)).StoredName;
        File.Delete(Path.Combine(_storageDir, storedName));

        var action = () => DownloadHandler(context, Store()).Handle(
            new GetTaskEvidenceDownloadQuery(uploaded.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*ausente no armazenamento*");
    }

    // ── Delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ShouldRemoveRowFileAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var uploaded = await UploadHandler(context, Store(), history).Handle(
            new UploadTaskEvidenceCommand(taskId, "temporario.txt", "text/plain", Bytes("remover")),
            CancellationToken.None);
        history.Records.Clear();

        var storedName = (await context.TaskEvidences.AsNoTracking()
            .SingleAsync(evidence => evidence.Id == uploaded.Id)).StoredName;
        var path = Path.Combine(_storageDir, storedName);

        await DeleteHandler(context, Store(), history).Handle(
            new DeleteTaskEvidenceCommand(uploaded.Id), CancellationToken.None);

        (await context.TaskEvidences.AsNoTracking()
            .CountAsync(evidence => evidence.Id == uploaded.Id))
            .Should().Be(0);
        File.Exists(path).Should().BeFalse();

        history.Records.Should().ContainSingle();
        history.Records[0].TaskId.Should().Be(taskId);
        history.Records[0].Action.Should().Be(TaskHistoryAction.EvidenceRemoved);
        history.Records[0].Detail.Should().Be("temporario.txt (1 kB)");
    }

    [Fact]
    public async Task Delete_WithFileAlreadyMissing_ShouldStillRemoveRow()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var uploaded = await UploadHandler(context, Store(), history).Handle(
            new UploadTaskEvidenceCommand(taskId, "orfao.txt", "text/plain", Bytes("x")),
            CancellationToken.None);

        var storedName = (await context.TaskEvidences.AsNoTracking()
            .SingleAsync(evidence => evidence.Id == uploaded.Id)).StoredName;
        File.Delete(Path.Combine(_storageDir, storedName));

        // Store engole o erro de IO (banco = fonte de verdade: ADR-033).
        var action = () => DeleteHandler(context, Store(), history).Handle(
            new DeleteTaskEvidenceCommand(uploaded.Id), CancellationToken.None);

        await action.Should().NotThrowAsync();
        (await context.TaskEvidences.AsNoTracking()
            .CountAsync(evidence => evidence.Id == uploaded.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task Delete_UnknownEvidence_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context, Store(), new RecordingTaskHistoryWriter())
            .Handle(new DeleteTaskEvidenceCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Evidência '{unknown}' não encontrada.");
    }

    // ── Query ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_ShouldListByUploadOrder()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var store = Store();
        var history = new RecordingTaskHistoryWriter();

        var first = await UploadHandler(context, store, history).Handle(
            new UploadTaskEvidenceCommand(taskId, "primeiro.txt", "text/plain", Bytes("1")),
            CancellationToken.None);
        // CreateDateTime pode cair no mesmo tick — espera entre uploads.
        await Task.Delay(20);
        var second = await UploadHandler(context, store, history).Handle(
            new UploadTaskEvidenceCommand(taskId, "segundo.txt", "text/plain", Bytes("2")),
            CancellationToken.None);

        var rows = await QueryHandler(context).Handle(
            new GetTaskEvidenceQuery(taskId), CancellationToken.None);

        rows.Select(evidence => evidence.Id)
            .Should().ContainInOrder(first.Id, second.Id);
        rows.Should().OnlyContain(evidence => evidence.TaskId == taskId);
        rows.Select(evidence => evidence.FileName)
            .Should().NotContain(storedName => storedName.Contains('/'));
    }

    [Fact]
    public async Task Query_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetTaskEvidenceQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}
