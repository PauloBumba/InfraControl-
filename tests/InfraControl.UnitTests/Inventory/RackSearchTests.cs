using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Racks.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F29-T001A — piloto da paginação compartilhada: Racks foi escolhido porque é
/// o slice mais idêntico ao resto (Location, Vlan, Connection repetem o mesmo
/// esqueleto linha a linha). Antes desta task, quem chamasse
/// `IRackRepository.SearchAsync` direto — outro handler, um worker — recebia
/// o que pedisse: `pageSize: 0` virava `Take(0)` (lista vazia "silenciosa")
/// e `page: 0` virava `Skip((0-1)*0)`, que só não quebrava por acaso.
/// O repositório passa a normalizar sozinho.
/// </summary>
public sealed class RackSearchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RackSearchTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, Location Location)> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var location = Location.Create("Sala Rack");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return (context, location);
    }

    [Fact]
    public async Task SearchAsync_WithNonsensePaging_ShouldClampInsteadOfReturningEmpty()
    {
        var (context, location) = await CreateContextAsync();
        context.Racks.Add(Rack.Create(location.Id, "Rack A"));
        await context.SaveChangesAsync();
        var repository = new RackRepository(context);

        var page = await repository.SearchAsync(
            locationId: null, term: null, page: 0, pageSize: 0);

        page.Items.Should().ContainSingle();
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_WithPageSizeAboveTheCap_ShouldNotFetchTheWholeTable()
    {
        var (context, location) = await CreateContextAsync();
        context.Racks.AddRange(
            Rack.Create(location.Id, "Rack A"),
            Rack.Create(location.Id, "Rack B"),
            Rack.Create(location.Id, "Rack C"));
        await context.SaveChangesAsync();
        var repository = new RackRepository(context);

        var page = await repository.SearchAsync(
            locationId: null, term: null, page: 1, pageSize: 5_000);

        // O total responde "quantos existem"; a página nunca entrega mais que o teto.
        page.TotalCount.Should().Be(3);
        page.PageSize.Should().Be(Paging.MaxPageSize);
    }

    [Fact]
    public async Task Search_WithPaddedTermAndHugePageSize_ShouldTrimAndCapEndToEnd()
    {
        var (context, location) = await CreateContextAsync();
        context.Racks.AddRange(
            Rack.Create(location.Id, "Rack A"),
            Rack.Create(location.Id, "Rack B"));
        await context.SaveChangesAsync();
        var handler = new SearchRacksQueryHandler(new RackRepository(context));

        var page = await handler.Handle(
            new SearchRacksQuery(Term: "  rack a  ", PageSize: 5_000),
            CancellationToken.None);

        var dto = page.Items.Should().ContainSingle().Subject;
        dto.Name.Should().Be("Rack A");
        page.TotalCount.Should().Be(1);
        page.PageSize.Should().Be(Paging.MaxPageSize);
    }

    [Fact]
    public async Task Search_WithBlankTerm_ShouldReturnEverything()
    {
        var (context, location) = await CreateContextAsync();
        context.Racks.Add(Rack.Create(location.Id, "Rack A"));
        await context.SaveChangesAsync();
        var handler = new SearchRacksQueryHandler(new RackRepository(context));

        var page = await handler.Handle(
            new SearchRacksQuery(Term: "   "), CancellationToken.None);

        page.Items.Should().ContainSingle();
    }
}
