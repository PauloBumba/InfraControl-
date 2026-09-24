using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Locations;
using InfraControl.Application.Inventory.Locations.Commands;
using InfraControl.Application.Inventory.Racks;
using InfraControl.Application.Inventory.Racks.Commands;
using InfraControl.Application.Inventory.Racks.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T002: handlers do slice Rack com repositório real em Sqlite.
/// Mapeamento esperado no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409. Nome único POR LOCAL (case-insensitive).
/// </summary>
public sealed class RackCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RackCommandTests()
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

    private static async Task<Guid> SeedLocationAsync(AppDbContext context, string name = "Data Center A")
    {
        var location = Location.Create(name);
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    private static CreateRackCommandHandler CreateHandler(AppDbContext context)
        => new(
            new RackRepository(context),
            new LocationRepository(context),
            NullLogger<CreateRackCommandHandler>.Instance);

    private static UpdateRackCommandHandler UpdateHandler(AppDbContext context)
        => new(new RackRepository(context), NullLogger<UpdateRackCommandHandler>.Instance);

    private static DeleteRackCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new RackRepository(context),
            new DeviceRepository(context),
            NullLogger<DeleteRackCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01", "Coluna frontal"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("R01");
        result.Description.Should().Be("Coluna frontal");
        context.Racks.Should().ContainSingle(rack => rack.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownLocation_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownLocationId = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateRackCommand(unknownLocationId, "R01"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Local '{unknownLocationId}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateNameInSameLocation_IgnoringCase_ShouldThrowConflictWithRackName()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "  r01  "), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um rack 'r01' neste local.");
    }

    [Fact]
    public async Task Create_SameNameInDifferentLocation_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var firstLocation = await SeedLocationAsync(context, "Data Center A");
        var secondLocation = await SeedLocationAsync(context, "Data Center B");
        await CreateHandler(context).Handle(
            new CreateRackCommand(firstLocation, "R01"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateRackCommand(secondLocation, "R01"), CancellationToken.None);

        var dto = (await action.Should().NotThrowAsync()).Which;
        dto.LocationId.Should().Be(secondLocation);
    }

    [Fact]
    public async Task Create_InvalidName_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "   "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome do rack é obrigatório.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingRack_ShouldChangeNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateRackCommand(created.Id, "R10", "Reposicionado"),
            CancellationToken.None);

        var stored = await context.Racks.SingleAsync(rack => rack.Id == created.Id);
        stored.Name.Should().Be("R10");
        stored.Description.Should().Be("Reposicionado");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateRackCommand(unknownId, "R01"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Rack '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToNameOfAnotherRackInSameLocation_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R02"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateRackCommand(second.Id, "R01"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um rack 'R01' neste local.");
    }

    [Fact]
    public async Task Update_ToItsOwnName_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateRackCommand(created.Id, "r01"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingRack_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteRackCommand(created.Id), CancellationToken.None);

        context.Racks.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RackWithDevices_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var rack = Rack.Create(locationId, "R01");
        context.Racks.Add(rack);
        context.Devices.Add(Device.Create(
            locationId, "sw-01", DeviceType.Switch, rackId: rack.Id));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteRackCommand(rack.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o rack: existem devices vinculados.");
        context.Racks.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteRackCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Rack '{unknownId}' não encontrado.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByLocation()
    {
        await using var context = await CreateContextAsync();
        var firstLocation = await SeedLocationAsync(context, "Data Center A");
        var secondLocation = await SeedLocationAsync(context, "Data Center B");
        await CreateHandler(context).Handle(
            new CreateRackCommand(firstLocation, "R01"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateRackCommand(firstLocation, "R02"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateRackCommand(secondLocation, "R01"), CancellationToken.None);

        var handler = new SearchRacksQueryHandler(new RackRepository(context));
        var scoped = await handler.Handle(
            new SearchRacksQuery(LocationId: firstLocation), CancellationToken.None);
        var all = await handler.Handle(new SearchRacksQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(2, "o filtro por local deve restringir os racks");
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ShouldFilterByNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "Core A"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "Borda", "fica no corredor norte"), CancellationToken.None);

        var handler = new SearchRacksQueryHandler(new RackRepository(context));
        var byName = await handler.Handle(new SearchRacksQuery(Term: "CORE"), CancellationToken.None);
        var byDescription = await handler.Handle(new SearchRacksQuery(Term: "corredor"), CancellationToken.None);

        byName.TotalCount.Should().Be(1, "termo deve casar no nome (case-insensitive) sem vazar para descrições");
        byDescription.TotalCount.Should().Be(1, "termo deve casar na descrição");
    }

    [Fact]
    public async Task Search_ShouldPageOrderedByNameWithinLocation()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        foreach (var name in new[] { "Zeta", "Alfa", "Mike" })
            await CreateHandler(context).Handle(
                new CreateRackCommand(locationId, name), CancellationToken.None);

        var handler = new SearchRacksQueryHandler(new RackRepository(context));
        var page1 = await handler.Handle(
            new SearchRacksQuery(LocationId: locationId, Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchRacksQuery(LocationId: locationId, Page: 2, PageSize: 2), CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(item => item.Name).Should().Equal("Alfa", "Mike");
        page2.Items.Select(item => item.Name).Should().Equal("Zeta");
    }

    [Fact]
    public async Task GetById_ExistingRack_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateRackCommand(locationId, "R01", " frontal"), CancellationToken.None);

        var handler = new GetRackQueryHandler(new RackRepository(context));
        var result = await handler.Handle(new GetRackQuery(created.Id), CancellationToken.None);

        result.Should().Be(new RackDto(created.Id, locationId, "R01", "frontal"));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetRackQueryHandler(new RackRepository(context));
        var action = () => handler.Handle(new GetRackQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Rack '{unknownId}' não encontrado.");
    }
}
