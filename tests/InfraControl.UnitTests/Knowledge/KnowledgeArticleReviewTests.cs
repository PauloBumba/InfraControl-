using FluentAssertions;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T005/K5: revisão periódica (spec docs/32 §6 governança + §12) —
/// agenda por artigo (intervalo + responsável), próxima revisão
/// calculada, ação "manter" reagenda, edição reagenda, filtro
/// "pendentes de revisão" e o alerta PendingReview no DTO.
/// </summary>
public sealed class KnowledgeArticleReviewTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleReviewTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        if (!await context.DomainUsers.AnyAsync(user => user.Id == CurrentUserId))
        {
            context.DomainUsers.Add(User.Create(
                CurrentUserId, "autor@exemplo.test", "Autor do Teste",
                UserRole.Operator));
            await context.SaveChangesAsync();
        }
        return context;
    }

    private sealed class FakeCurrentUser(string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = CurrentUserId;
        public string? UserName => "autor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static CreateKnowledgeArticleCommandHandler CreateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<CreateKnowledgeArticleCommandHandler>.Instance);

    private static UpdateKnowledgeArticleCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<UpdateKnowledgeArticleCommandHandler>.Instance);

    private static SetKnowledgeArticleReviewScheduleCommandHandler SetScheduleHandler(
        AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            NullLogger<SetKnowledgeArticleReviewScheduleCommandHandler>.Instance);

    private static ConfirmKnowledgeArticleReviewCommandHandler ConfirmHandler(
        AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            NullLogger<ConfirmKnowledgeArticleReviewCommandHandler>.Instance);

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static async Task<Guid> SeedArticleAsync(AppDbContext context, string title)
    {
        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(title, "Conteúdo.", null),
            CancellationToken.None);
        return dto.Id;
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext context, string name)
    {
        var id = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(
            id, $"{name.ToLowerInvariant()}@exemplo.test", name, UserRole.Operator));
        await context.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Revisa a data "vencida" direto no banco: sem relógio falso
    /// global (AppTimeProvider é estático e os testes rodam em
    /// paralelo), o SQL é o jeito seguro de envelhecer a linha.
    /// </summary>
    private static Task BackdateReviewAsync(AppDbContext context, Guid articleId)
        => context.Database.ExecuteSqlRawAsync(
            "UPDATE knowledge_articles SET \"NextReviewAt\" = datetime('now', '-1 day') "
            + "WHERE id = {0}",
            articleId);

    [Fact]
    public async Task SetSchedule_ShouldComputeNextReview_WithResponsibleAndDto()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");
        var reviewerId = await SeedUserAsync(context, "Revisora");

        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 180, reviewerId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        var expected = DateTime.UtcNow.Date.AddDays(180);

        dto.ReviewIntervalDays.Should().Be(180);
        dto.NextReviewAt.Should().NotBeNull();
        dto.NextReviewAt!.Value.Date.Should().Be(expected);
        dto.ReviewResponsibleId.Should().Be(reviewerId);
        dto.ReviewResponsibleName.Should().Be("Revisora");
        dto.PendingReview.Should().BeFalse(); // ainda não venceu
    }

    [Fact]
    public async Task SetSchedule_ZeroInterval_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");

        var action = () => SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 0, null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage(
                "O intervalo de revisão deve estar entre 1 e 3650 dias.*");
    }

    [Fact]
    public async Task SetSchedule_TooLongInterval_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");

        var action = () => SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 3651, null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage(
                "O intervalo de revisão deve estar entre 1 e 3650 dias.*");
    }

    [Fact]
    public async Task SetSchedule_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");
        var unknown = Guid.NewGuid();

        var action = () => SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 180, unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task SetSchedule_NullInterval_ShouldClearSchedule()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");
        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 90, null),
            CancellationToken.None);

        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, null, null),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.ReviewIntervalDays.Should().BeNull();
        dto.NextReviewAt.Should().BeNull();
        dto.PendingReview.Should().BeFalse();
    }

    /// <summary>§12: quando chegar a data, "Knowledge necessita de
    /// revisão" → ação "manter" reagenda.</summary>
    [Fact]
    public async Task ConfirmReview_AfterDueDate_ShouldRescheduleAndClearAlert()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");
        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 180, null),
            CancellationToken.None);
        await BackdateReviewAsync(context, articleId);

        await ConfirmHandler(context).Handle(
            new ConfirmKnowledgeArticleReviewCommand(articleId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.PendingReview.Should().BeFalse();
        dto.NextReviewAt!.Value.Date.Should().Be(DateTime.UtcNow.Date.AddDays(180));
    }

    [Fact]
    public async Task ConfirmReview_WithoutSchedule_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");

        var action = () => ConfirmHandler(context).Handle(
            new ConfirmKnowledgeArticleReviewCommand(articleId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O artigo não tem revisão periódica configurada.");
    }

    /// <summary>§12: ação "atualizar" — editar o artigo reagenda a
    /// próxima revisão sozinho.</summary>
    [Fact]
    public async Task Update_ShouldPostponeReview()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, "Roteiro");
        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(articleId, 180, null),
            CancellationToken.None);
        await BackdateReviewAsync(context, articleId);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro", "Conteúdo revisado.", null,
                "Revisão da rotina."),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.PendingReview.Should().BeFalse();
        dto.NextReviewAt!.Value.Date.Should().Be(DateTime.UtcNow.Date.AddDays(180));
    }

    /// <summary>Filtro "pendentes de revisão": só o vencido volta.</summary>
    [Fact]
    public async Task Search_PendingReviewFilter_ShouldReturnOnlyDueArticles()
    {
        await using var context = await CreateContextAsync();
        var dueId = await SeedArticleAsync(context, "Vencido");
        var futureId = await SeedArticleAsync(context, "Em dia");
        var noScheduleId = await SeedArticleAsync(context, "Sem agenda");

        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(dueId, 30, null),
            CancellationToken.None);
        await BackdateReviewAsync(context, dueId);
        await SetScheduleHandler(context).Handle(
            new SetKnowledgeArticleReviewScheduleCommand(futureId, 30, null),
            CancellationToken.None);

        var page = await SearchHandler(context).Handle(
            new SearchKnowledgeArticlesQuery(PendingReview: true),
            CancellationToken.None);

        page.Items.Should().ContainSingle()
            .Which.Id.Should().Be(dueId);
        page.Items.Should().NotContain(article => article.Id == futureId);
        page.Items.Should().NotContain(article => article.Id == noScheduleId);
        page.Items.Single().PendingReview.Should().BeTrue();
    }
}
