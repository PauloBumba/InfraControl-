using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Locations;
using InfraControl.Application.Inventory.Locations.Commands;
using InfraControl.Application.Inventory.Locations.Queries;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T001: handlers do slice Location com repositório real em Sqlite
/// (mesmo padrão dos testes de Identity). Mensagens de erro em pt-BR e
/// mapeamento esperado: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409 (envelope via GlobalExceptionHandler).
/// </summary>
public sealed class LocationCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public LocationCommandTests()
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

    private static CreateLocationCommandHandler CreateHandler(AppDbContext context)
        => new(new LocationRepository(context), NullLogger<CreateLocationCommandHandler>.Instance);

    private static UpdateLocationCommandHandler UpdateHandler(AppDbContext context)
        => new(new LocationRepository(context), NullLogger<UpdateLocationCommandHandler>.Instance);

    private static DeleteLocationCommandHandler DeleteHandler(AppDbContext context)
        => new(new LocationRepository(context), NullLogger<DeleteLocationCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();

        var result = await CreateHandler(context).Handle(
            new CreateLocationCommand("Data Center A", "Principal"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Data Center A");
        result.Description.Should().Be("Principal");
        context.Locations.Should().ContainSingle(location => location.Id == result.Id);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala Norte"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateLocationCommand("  sala norte  "), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um local com este nome.");
    }

    [Fact]
    public async Task Create_InvalidName_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateLocationCommand("   "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome do local é obrigatório.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingLocation_ShouldChangeNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala 1"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateLocationCommand(created.Id, "Sala 10", "Ampliada"),
            CancellationToken.None);

        var stored = await context.Locations.SingleAsync(location => location.Id == created.Id);
        stored.Name.Should().Be("Sala 10");
        stored.Description.Should().Be("Ampliada");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundWithPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateLocationCommand(unknownId, "Qualquer"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Local '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToNameOfAnotherLocation_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var first = await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala Norte"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala Sul"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateLocationCommand(second.Id, "SALA norte"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um local com este nome.");
    }

    [Fact]
    public async Task Update_ToItsOwnName_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala Norte"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateLocationCommand(created.Id, "sala NORTE", "mesma"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingLocation_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateLocationCommand("Descartável"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteLocationCommand(created.Id), CancellationToken.None);

        context.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundWithPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteLocationCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Local '{unknownId}' não encontrado.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByNameAndByDescription()
    {
        await using var context = await CreateContextAsync();
        var repository = new LocationRepository(context);
        await CreateHandler(context).Handle(new CreateLocationCommand("Sala Norte"), CancellationToken.None);
        await CreateHandler(context).Handle(new CreateLocationCommand("Sala Sul"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateLocationCommand("Copa", "no térreo junto ao hall"), CancellationToken.None);

        var handler = new SearchLocationsQueryHandler(repository);
        var byName = await handler.Handle(new SearchLocationsQuery("SALA"), CancellationToken.None);
        var byDescription = await handler.Handle(new SearchLocationsQuery("térreo"), CancellationToken.None);

        byName.TotalCount.Should().Be(2, "o termo deve casar no nome (case-insensitive)");
        byDescription.TotalCount.Should().Be(1, "o termo deve casar também na descrição");
        byDescription.Items.Single().Name.Should().Be("Copa");
    }

    [Fact]
    public async Task Search_WithoutTerm_ShouldPageOrderedByName()
    {
        await using var context = await CreateContextAsync();
        var repository = new LocationRepository(context);
        foreach (var name in new[] { "Zeta", "Alfa", "Mike" })
            await CreateHandler(context).Handle(new CreateLocationCommand(name), CancellationToken.None);

        var handler = new SearchLocationsQueryHandler(repository);
        var page1 = await handler.Handle(
            new SearchLocationsQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchLocationsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(item => item.Name).Should().Equal("Alfa", "Mike");
        page2.Items.Select(item => item.Name).Should().Equal("Zeta");
    }

    [Fact]
    public async Task GetById_ExistingLocation_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateLocationCommand("Sala 7", "Torre B"), CancellationToken.None);

        var handler = new GetLocationQueryHandler(new LocationRepository(context));
        var result = await handler.Handle(new GetLocationQuery(created.Id), CancellationToken.None);

        result.Should().Be(new LocationDto(created.Id, "Sala 7", "Torre B"));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundWithPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetLocationQueryHandler(new LocationRepository(context));
        var action = () => handler.Handle(new GetLocationQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Local '{unknownId}' não encontrado.");
    }
}
