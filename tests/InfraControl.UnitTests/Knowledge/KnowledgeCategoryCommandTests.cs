using FluentAssertions;
using InfraControl.Application.Knowledge.Categories;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.Application.Knowledge.Categories.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T001: handlers de KnowledgeCategory — CRUD, nome duplicado (409),
/// 404 e busca.
/// </summary>
public sealed class KnowledgeCategoryCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeCategoryCommandTests()
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

    private static CreateKnowledgeCategoryCommandHandler CreateHandler(AppDbContext context)
        => new(new KnowledgeCategoryRepository(context), NullLogger<CreateKnowledgeCategoryCommandHandler>.Instance);

    private static UpdateKnowledgeCategoryCommandHandler UpdateHandler(AppDbContext context)
        => new(new KnowledgeCategoryRepository(context), NullLogger<UpdateKnowledgeCategoryCommandHandler>.Instance);

    private static DeleteKnowledgeCategoryCommandHandler DeleteHandler(AppDbContext context)
        => new(new KnowledgeCategoryRepository(context), NullLogger<DeleteKnowledgeCategoryCommandHandler>.Instance);

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();

        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", "Topologia e conectividade"),
            CancellationToken.None);

        dto.Name.Should().Be("Redes");
        dto.Description.Should().Be("Topologia e conectividade");
        var saved = await context.KnowledgeCategories.AsNoTracking().SingleAsync(c => c.Id == dto.Id);
        saved.Name.Should().Be("Redes");
    }

    [Fact]
    public async Task Create_DuplicateName_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", null), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("  redes  ", null), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma categoria com o nome 'redes'.");
    }

    [Fact]
    public async Task Create_EmptyName_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("   ", null), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da categoria é obrigatório.*");
    }

    [Fact]
    public async Task Update_Existing_ShouldChangeNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", null), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeCategoryCommand(created.Id, "Segurança", "Novo foco"),
            CancellationToken.None);

        var saved = await context.KnowledgeCategories.AsNoTracking().SingleAsync(c => c.Id == created.Id);
        saved.Name.Should().Be("Segurança");
        saved.Description.Should().Be("Novo foco");
    }

    [Fact]
    public async Task Update_Unknown_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeCategoryCommand(unknown, "Qualquer", null), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Update_ToDuplicateName_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var first = await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", null), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Backup", null), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeCategoryCommand(first.Id, "backup", null), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma categoria com o nome 'backup'.");
    }

    [Fact]
    public async Task Delete_Existing_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", null), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteKnowledgeCategoryCommand(created.Id), CancellationToken.None);

        (await context.KnowledgeCategories.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_Unknown_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteKnowledgeCategoryCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Search_ShouldFilterByTerm()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Redes", null), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateKnowledgeCategoryCommand("Backup", null), CancellationToken.None);

        var page = await new SearchKnowledgeCategoriesQueryHandler(
                new KnowledgeCategoryRepository(context))
            .Handle(new SearchKnowledgeCategoriesQuery(Term: "red"), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Name.Should().Be("Redes");
    }
}
