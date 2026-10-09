using FluentAssertions;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Pdi;

/// <summary>
/// Fase 4 do PDI (docs/30 §12, §17, §23 PDIEvidence, ADR-034): upload
/// de evidências genéricas sob a fase — 9 tipos fixos, fluxo pendente →
/// validada/rejeitada (motivo obrigatório), download autenticado e
/// registro no timeline. Escopo: envio dono/Admin, revisão gestor/Admin,
/// arquivo com nome saneado e limite de tamanho, revisão única.
/// </summary>
public sealed class PdiEvidenceTests
{
    [Fact]
    public async Task Upload_ShouldPersistEvidenceFileAndHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var handler = EvidenceUpload(context, scene.Owner, store);

        var created = await handler.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Certificate,
                "Conclusão do curso de liderança",
                "pasta/certificado.pdf", "application/pdf",
                FileSize: 1024, Content: Bytes(1024)),
            CancellationToken.None);

        created.Status.Should().Be(nameof(PdiEvidenceStatus.Pending));
        created.Type.Should().Be(nameof(PdiEvidenceType.Certificate));
        created.FileName.Should().Be("certificado.pdf");
        created.Description.Should().Be("Conclusão do curso de liderança");
        created.CreatedByName.Should().Be("Ana Dono");
        created.ReviewedBy.Should().BeNull();

        store.Files.Should().ContainKey(created.Id);
        store.Files[created.Id].Ext.Should().Be(".pdf");
        store.Files[created.Id].Data.Should().HaveCount(1024);

        (await context.PdiEvidences.ToListAsync()).Should().ContainSingle();

        var history = await context.PdiHistoryEntries
            .Where(entry => entry.Type == PdiHistoryType.EvidenceAdded)
            .ToListAsync();
        history.Should().ContainSingle();
        history[0].ActorId.Should().Be(scene.Owner.UserId!.Value);
        history[0].Detail.Should().Contain("certificado");
    }

    [Fact]
    public async Task Upload_WhenUnknownPhaseOrForeignOperator_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var handler = EvidenceUpload(context, scene.Owner, store);

        var missing = () => handler.Handle(
            new UploadPdiEvidenceCommand(
                Guid.NewGuid(), PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 10, Bytes(10)),
            CancellationToken.None);
        await missing.Should().ThrowAsync<KeyNotFoundException>();

        var intruder = EvidenceUpload(
            context, new FakeCurrentUser(Guid.NewGuid(), "Operator"), store);
        var foreign = () => intruder.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 10, Bytes(10)),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");
    }

    [Fact]
    public async Task Upload_WhenTypeOrFileInvalid_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var handler = EvidenceUpload(context, scene.Owner, store);

        var badType = () => handler.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, (PdiEvidenceType)99, null,
                "doc.pdf", null, 10, Bytes(10)),
            CancellationToken.None);
        await badType.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tipo*");

        var emptyName = () => handler.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "   ", null, 10, Bytes(10)),
            CancellationToken.None);
        await emptyName.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*arquivo*");

        var zeroSize = () => handler.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", null, 0, Bytes(0)),
            CancellationToken.None);
        await zeroSize.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*arquivo*");

        var tooBig = () => handler.Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", null, PdiEvidence.MaxFileSize + 1,
                Bytes(1)),
            CancellationToken.None);
        await tooBig.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tamanho*");

        (await context.PdiEvidences.ToListAsync()).Should().BeEmpty();
        store.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task Review_WhenApproved_ShouldMarkValidatedAndRecordHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Result, "Certificado emitido",
                "resultado.pdf", "application/pdf", 20, Bytes(20)),
            CancellationToken.None);

        var reviewed = await EvidenceReview(context, scene.Manager).Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);

        reviewed.Status.Should().Be(nameof(PdiEvidenceStatus.Validated));
        reviewed.ReviewedBy.Should().Be(scene.Manager.UserId!.Value);
        reviewed.ReviewedAt.Should().NotBeNull();

        var history = await context.PdiHistoryEntries
            .Where(entry => entry.Type == PdiHistoryType.EvidenceReviewed)
            .ToListAsync();
        history.Should().ContainSingle();
        history[0].Detail.Should().Be("validada");
        history[0].ActorId.Should().Be(scene.Manager.UserId!.Value);
    }

    [Fact]
    public async Task Review_WhenRejected_ShouldRequireReason()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 30, Bytes(30)),
            CancellationToken.None);

        var handler = EvidenceReview(context, scene.Manager);
        var noReason = () => handler.Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Rejected, "  "),
            CancellationToken.None);
        await noReason.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*motivo*");

        var rejected = await handler.Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Rejected,
                "Arquivo ilegível"),
            CancellationToken.None);

        rejected.Status.Should().Be(nameof(PdiEvidenceStatus.Rejected));
        rejected.RejectionReason.Should().Be("Arquivo ilegível");

        var history = await context.PdiHistoryEntries
            .Where(entry => entry.Type == PdiHistoryType.EvidenceReviewed)
            .ToListAsync();
        history.Should().ContainSingle();
        history[0].Detail.Should().Be("rejeitada");
        history[0].Reason.Should().Be("Arquivo ilegível");
    }

    [Fact]
    public async Task Review_WhenByOwnerOrStranger_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 10, Bytes(10)),
            CancellationToken.None);

        var byOwner = () => EvidenceReview(context, scene.Owner).Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);
        await byOwner.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*gestor*");

        var stranger = EvidenceReview(
            context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var byStranger = () => stranger.Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);
        await byStranger.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Review_WhenAlreadyReviewed_ShouldConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 10, Bytes(10)),
            CancellationToken.None);

        var handler = EvidenceReview(context, scene.Manager);
        await handler.Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);

        var twice = () => handler.Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Rejected, "motivo"),
            CancellationToken.None);
        await twice.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Review_WhenAdmin_ShouldApproveWithoutManager()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context, withManager: false);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Document, null,
                "doc.pdf", "application/pdf", 10, Bytes(10)),
            CancellationToken.None);

        var adminUser = await SeedUserAsync(
            context, "admin@exemplo.test", "Alice Admin");
        var admin = new FakeCurrentUser(adminUser.Id, "Admin");

        var reviewed = await EvidenceReview(context, admin).Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);
        reviewed.Status.Should().Be(nameof(PdiEvidenceStatus.Validated));

        // Sem gestor no plano, um Operator comum não revisa.
        var other = new FakeCurrentUser(Guid.NewGuid(), "Operator");
        var byOther = () => EvidenceReview(context, other).Handle(
            new ReviewPdiEvidenceCommand(
                uploaded.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);
        await byOther.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Download_WhenViewer_ShouldReturnFileAndBlockForeigners()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        var uploaded = await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Image, null,
                "foto.png", "image/png", 5, Bytes(5)),
            CancellationToken.None);

        var download = await EvidenceDownload(context, scene.Manager, store).Handle(
            new DownloadPdiEvidenceQuery(uploaded.Id), CancellationToken.None);
        download.FileName.Should().Be("foto.png");
        download.ContentType.Should().Be("image/png");
        using (var memory = new MemoryStream())
        {
            await download.Content.CopyToAsync(memory);
            memory.ToArray().Should().HaveCount(5);
        }

        var stranger = EvidenceDownload(
            context, new FakeCurrentUser(Guid.NewGuid(), "Operator"), store);
        var foreign = () => stranger.Handle(
            new DownloadPdiEvidenceQuery(uploaded.Id), CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>();

        var unknown = () => EvidenceDownload(context, scene.Manager, store).Handle(
            new DownloadPdiEvidenceQuery(Guid.NewGuid()),
            CancellationToken.None);
        await unknown.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Accompaniment_ShouldIncludeUploadedEvidences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var store = new FakeFileStore();
        await EvidenceUpload(context, scene.Owner, store).Handle(
            new UploadPdiEvidenceCommand(
                scene.PhaseId, PdiEvidenceType.Certificate, null,
                "cert.pdf", "application/pdf", 7, Bytes(7)),
            CancellationToken.None);

        var dto = await AccompanimentQuery(context, scene.Manager).Handle(
            new GetPdiAccompanimentQuery(scene.Owner.UserId!.Value),
            CancellationToken.None);

        dto.Evidences.Should().ContainSingle();
        dto.Evidences[0].FileName.Should().Be("cert.pdf");
        dto.Evidences[0].CreatedByName.Should().Be("Ana Dono");
    }

    // ---- Fixtures -------------------------------------------------

    private sealed record SceneActors(
        FakeCurrentUser Owner,
        FakeCurrentUser Manager,
        Guid PhaseId);

    private static async Task<SceneActors> SeedSceneAsync(
        AppDbContext context, bool withManager = true)
    {
        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id, withManager ? manager.Id : null);

        var ownerActor = new FakeCurrentUser(owner.Id, "Operator");
        var managerActor = new FakeCurrentUser(manager.Id, "Operator");

        var objective = await ObjectiveCreate(context, ownerActor).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Assumir liderança"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, ownerActor).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Formação em gestão"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, ownerActor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Primeiros passos"),
            CancellationToken.None);

        return new SceneActors(ownerActor, managerActor, phase.Id);
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<IndividualDevelopmentPlan> SeedPlanAsync(
        AppDbContext context, Guid ownerId, Guid? managerId)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId, "Plano do ciclo", new DateOnly(2027, 6, 30),
            PdiStatus.Active, managerId, null, null,
            new PdiChangeActor(ownerId, $"dono-{ownerId}", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    private static Stream Bytes(int count)
        => new MemoryStream(
            Enumerable.Range(0, count).Select(i => (byte)(i % 256)).ToArray());

    private static UploadPdiEvidenceCommandHandler EvidenceUpload(
        AppDbContext context, FakeCurrentUser user, FakeFileStore store)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, store, NullLogger<UploadPdiEvidenceCommandHandler>.Instance);

    private static ReviewPdiEvidenceCommandHandler EvidenceReview(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<ReviewPdiEvidenceCommandHandler>.Instance);

    private static DownloadPdiEvidenceQueryHandler EvidenceDownload(
        AppDbContext context, FakeCurrentUser user, FakeFileStore store)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, store);

    private static GetPdiAccompanimentQueryHandler AccompanimentQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiPhaseCommandHandler>.Instance);

    private sealed class FakeFileStore : IPdiEvidenceFileStore
    {
        internal Dictionary<Guid, (string Ext, byte[] Data)> Files { get; } = [];

        public Task SaveAsync(
            Guid evidenceId, string extension, Stream content,
            CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            content.CopyTo(memory);
            Files[evidenceId] = (extension, memory.ToArray());
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
        {
            if (!Files.TryGetValue(evidenceId, out var file))
                throw new KeyNotFoundException(
                    $"Arquivo da evidência '{evidenceId}' não encontrado.");
            return Task.FromResult<Stream>(new MemoryStream(file.Data));
        }

        public Task DeleteAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
        {
            Files.Remove(evidenceId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public Guid Id => UserId!.Value;
        public string? UserName => $"ator-{UserId}@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
