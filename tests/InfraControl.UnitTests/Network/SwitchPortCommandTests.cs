using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Network.SwitchPorts;
using InfraControl.Application.Network.SwitchPorts.Commands;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T002: handlers do slice SwitchPort em Sqlite (sem boilerplate de using
/// — Fase 1 global usings). Reuso do guard de switch do F3-T001 via
/// GetSwitchQueryHandler (device que não é switch → 404 pt-BR). Mapeamento no
/// envelope: ArgumentException → 400, KeyNotFound → 404, InvalidOperation → 409.
/// </summary>
public sealed class SwitchPortCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SwitchPortCommandTests()
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

    private static async Task<Guid> SeedSwitchAsync(
        AppDbContext context,
        string deviceName = "sw-01",
        string locationName = "Data Center A")
    {
        var location = Location.Create(locationName);
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, deviceName, DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static async Task<Guid> SeedPrinterAsync(AppDbContext context)
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "pr-01", DeviceType.Printer);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static CreateSwitchPortCommandHandler CreateHandler(AppDbContext context)
        => new(
            new SwitchPortRepository(context),
            new GetSwitchQueryHandler(new DeviceRepository(context)),
            NullLogger<CreateSwitchPortCommandHandler>.Instance);

    private static UpdateSwitchPortCommandHandler UpdateHandler(AppDbContext context)
        => new(new SwitchPortRepository(context), NullLogger<UpdateSwitchPortCommandHandler>.Instance);

    private static DeleteSwitchPortCommandHandler DeleteHandler(AppDbContext context)
        => new(new SwitchPortRepository(context), NullLogger<DeleteSwitchPortCommandHandler>.Instance);

    private static SearchSwitchPortsQueryHandler SearchHandler(AppDbContext context)
        => new(
            new SwitchPortRepository(context),
            new GetSwitchQueryHandler(new DeviceRepository(context)));

    private static GetSwitchPortQueryHandler GetHandler(AppDbContext context)
        => new(new SwitchPortRepository(context));

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistPortOnSwitchAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1", "Uplink p/ core"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.DeviceId.Should().Be(switchId);
        result.Name.Should().Be("Gi1/0/1");
        result.Description.Should().Be("Uplink p/ core");
        result.LinkUp.Should().BeNull();
        context.SwitchPorts.Should().ContainSingle(port => port.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownSwitch = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateSwitchPortCommand(unknownSwitch, "Gi1/0/1"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Switch '{unknownSwitch}' não encontrado.");
    }

    [Fact]
    public async Task Create_OnPrinterDevice_ShouldThrowNotFoundPtBrMessage()
    {
        // Reuso do guard F3-T001: porta só existe em device tipo Switch.
        await using var context = await CreateContextAsync();
        var printerId = await SeedPrinterAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateSwitchPortCommand(printerId, "Gi1/0/1"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Switch '{printerId}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateNameInSameSwitch_CaseInsensitive_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "gi1/0/1"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma porta 'gi1/0/1' neste switch.",
                "nome da porta é único por switch, sem distinção de maiúsculas");
    }

    [Fact]
    public async Task Create_SameNameOnAnotherSwitch_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var firstSwitch = await SeedSwitchAsync(context, "sw-01", "Data Center A");
        var secondSwitch = await SeedSwitchAsync(context, "sw-02", "Data Center B");

        var first = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(firstSwitch, "Gi1/0/1"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(secondSwitch, "Gi1/0/1"), CancellationToken.None);

        second.Id.Should().NotBe(first.Id, "a unicidade do nome é POR SWITCH, não global");
        context.SwitchPorts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_WithEmptyName_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "  "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da porta é obrigatório.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingPort_ShouldApplyChanges()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateSwitchPortCommand(created.Id, "Gi1/0/2", "Porta de acesso", LinkUp: true),
            CancellationToken.None);

        var stored = await context.SwitchPorts.SingleAsync(port => port.Id == created.Id);
        stored.Name.Should().Be("Gi1/0/2");
        stored.Description.Should().Be("Porta de acesso");
        stored.LinkUp.Should().BeTrue();
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateSwitchPortCommand(unknownId, "Gi1/0/1"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToNameOfAnotherPortInSameSwitch_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/2"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateSwitchPortCommand(second.Id, "Gi1/0/1"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma porta 'Gi1/0/1' neste switch.");
    }

    [Fact]
    public async Task Update_ToItsOwnName_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateSwitchPortCommand(created.Id, "Gi1/0/1"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingPort_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteSwitchPortCommand(created.Id), CancellationToken.None);

        context.SwitchPorts.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteSwitchPortCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownId}' não encontrado.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldReturnPortsOfSwitchOrderedByName()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        foreach (var name in new[] { "Gi1/0/2", "Gi1/0/1", "Gi1/0/10" })
            await CreateHandler(context).Handle(
                new CreateSwitchPortCommand(switchId, name), CancellationToken.None);

        var result = await SearchHandler(context).Handle(
            new SearchSwitchPortsQuery(switchId), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Select(port => port.Name).Should().Equal(
            new[] { "Gi1/0/1", "Gi1/0/10", "Gi1/0/2" },
            "ordenação lexicográfica por nome (mesma regra das interfaces)");
    }

    [Fact]
    public async Task Search_ShouldFilterByTerm()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        foreach (var name in new[] { "Gi1/0/1", "Gi1/0/2" })
            await CreateHandler(context).Handle(
                new CreateSwitchPortCommand(switchId, name), CancellationToken.None);

        var result = await SearchHandler(context).Handle(
            new SearchSwitchPortsQuery(switchId, Term: "1/0/2"), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("Gi1/0/2");
    }

    [Fact]
    public async Task Search_OnPrinterDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var printerId = await SeedPrinterAsync(context);

        var action = () => SearchHandler(context).Handle(
            new SearchSwitchPortsQuery(printerId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Switch '{printerId}' não encontrado.",
                "listar portas de device que não é switch expõe erro do guard F3-T001");
    }

    [Fact]
    public async Task GetById_ExistingPort_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var switchId = await SeedSwitchAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateSwitchPortCommand(switchId, "Gi1/0/1", "Uplink"), CancellationToken.None);

        var result = await GetHandler(context).Handle(
            new GetSwitchPortQuery(created.Id), CancellationToken.None);

        result.Should().Be(new SwitchPortDto(created.Id, switchId, "Gi1/0/1", "Uplink", null));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => GetHandler(context).Handle(
            new GetSwitchPortQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Porta '{unknownId}' não encontrado.");
    }
}
