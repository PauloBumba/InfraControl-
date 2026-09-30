using FluentAssertions;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Application.Knowledge.Categories;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T002: handlers de KnowledgeArticle — CRUD com FK de categoria
/// (404), 404 de artigo, busca e exclusão de categoria com artigos (409).
/// </summary>
public sealed class KnowledgeArticleCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleCommandTests()
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

    private static CreateKnowledgeArticleCommandHandler CreateHandler(AppDbContext context)
        => new(new KnowledgeArticleRepository(context),
            NullLogger<CreateKnowledgeArticleCommandHandler>.Instance);

    private static UpdateKnowledgeArticleCommandHandler UpdateHandler(AppDbContext context)
        => new(new KnowledgeArticleRepository(context),
            NullLogger<UpdateKnowledgeArticleCommandHandler>.Instance);

    private static DeleteKnowledgeArticleCommandHandler DeleteHandler(AppDbContext context)
        => new(new KnowledgeArticleRepository(context),
            NullLogger<DeleteKnowledgeArticleCommandHandler>.Instance);

    private static async Task<Guid> SeedCategoryAsync(AppDbContext context, string name = "Redes")
    {
        var category = KnowledgeCategory.Create(name, null);
        context.KnowledgeCategories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    [Fact]
    public async Task Create_ShouldPersistAsDraftAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var categoryId = await SeedCategoryAsync(context);

        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Como trocar o switch", "Passo a passo.", categoryId),
            CancellationToken.None);

        dto.Status.Should().Be(KnowledgeArticleStatus.Draft);
        var saved = await context.KnowledgeArticles
            .AsNoTracking().SingleAsync(a => a.Id == dto.Id);
        saved.CategoryId.Should().Be(categoryId);
        saved.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task Create_UnknownCategory_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Órfão", "Conteúdo.", unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Create_EmptyTitle_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("  ", "Conteúdo.", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título do artigo é obrigatório.*");
    }

    [Fact]
    public async Task Update_Existing_ShouldChangeTitleBodyAndCategory()
    {
        await using var context = await CreateContextAsync();
        var categoryId = await SeedCategoryAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Título", "Conteúdo.", null),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Título v2", "Conteúdo v2.", categoryId),
            CancellationToken.None);

        var saved = await context.KnowledgeArticles
            .AsNoTracking().SingleAsync(a => a.Id == created.Id);
        saved.Title.Should().Be("Título v2");
        saved.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Update_UnknownArticle_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(unknown, "Título", "Conteúdo.", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Delete_Existing_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Título", "Conteúdo.", null),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteKnowledgeArticleCommand(created.Id), CancellationToken.None);

        (await context.KnowledgeArticles.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_UnknownArticle_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteKnowledgeArticleCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Search_ShouldFilterByTermAndStatus()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Switch principal", "Troca.", null),
            CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Backup diário", "Rotina.", null),
            CancellationToken.None);

        var page = await new SearchKnowledgeArticlesQueryHandler(
                new KnowledgeArticleRepository(context))
            .Handle(new SearchKnowledgeArticlesQuery(
                Term: "switch", Status: KnowledgeArticleStatus.Draft),
                CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Title.Should().Be("Switch principal");
    }

    /// <summary>Exclusão de categoria com artigos vinculados = 409 (FK Restrict é o backstop).</summary>
    [Fact]
    public async Task DeleteCategory_WithArticles_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var categoryId = await SeedCategoryAsync(context);
        await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Título", "Conteúdo.", categoryId),
            CancellationToken.None);

        var action = () => new DeleteKnowledgeCategoryCommandHandler(
                new KnowledgeCategoryRepository(context),
                new KnowledgeArticleRepository(context),
                NullLogger<DeleteKnowledgeCategoryCommandHandler>.Instance)
            .Handle(new DeleteKnowledgeCategoryCommand(categoryId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a categoria: existem artigos vinculados.");
    }
}
