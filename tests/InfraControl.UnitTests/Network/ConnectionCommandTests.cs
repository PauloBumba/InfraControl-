using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.NetworkInterfaces;
using InfraControl.Application.Inventory.NetworkInterfaces.Queries;
using InfraControl.Application.Network.Connections.Commands;
using InfraControl.Application.Network.Connections.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T005: CRUD de Connection (Interface N:N Interface via Connection,
/// docs/03) — par DIRIGIDO único, só self-loop proibido (decisões do
/// usuário). Mapeamento: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409.
/// </summary>
public sealed class ConnectionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ConnectionCommandTests()
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

    private static async Task<Guid> SeedInterfaceAsync(
        AppDbContext context,
        string deviceName,
        string nicName)
    {
        var location = await context.Locations.FirstOrDefaultAsync();
        if (location is null)
        {
            location = Location.Create("Data Center A");
            context.Locations.Add(location);
            await context.SaveChangesAsync();
        }

        var device = await context.Devices.FirstOrDefaultAsync(d => d.Name == deviceName);
        if (device is null)
        {
            device = Device.Create(location.Id, deviceName, DeviceType.Switch);
            context.Devices.Add(device);
            await context.SaveChangesAsync();
        }

        var nic = NetworkInterface.Create(device.Id, nicName);
        context.NetworkInterfaces.Add(nic);
        await context.SaveChangesAsync();
        return nic.Id;
    }

    private static CreateConnectionCommandHandler CreateHandler(AppDbContext context)
        => new(
            new ConnectionRepository(context),
            new GetNetworkInterfaceQueryHandler(new NetworkInterfaceRepository(context)),
            NullLogger<CreateConnectionCommandHandler>.Instance);

    private static DeleteConnectionCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new ConnectionRepository(context),
            NullLogger<DeleteConnectionCommandHandler>.Instance);

    private static UpdateConnectionCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new ConnectionRepository(context),
            new GetNetworkInterfaceQueryHandler(new NetworkInterfaceRepository(context)),
            NullLogger<UpdateConnectionCommandHandler>.Instance);

    private static SearchConnectionsQueryHandler SearchHandler(AppDbContext context)
        => new(new ConnectionRepository(context));

    private static GetConnectionQueryHandler GetHandler(AppDbContext context)
        => new(new ConnectionRepository(context));

    // ── Update (ADR-030 §1 — CRUD completo; PUT reaponta as pontas) ──────

    [Fact]
    public async Task Update_Existing_ShouldRepointAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var oldTargetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var newTargetId = await SeedInterfaceAsync(context, "sw-03", "Gi0/3");
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, oldTargetId), CancellationToken.None);

        var result = await UpdateHandler(context).Handle(
            new UpdateConnectionCommand(created.Id, sourceId, newTargetId),
            CancellationToken.None);

        result.Id.Should().Be(created.Id);
        result.TargetInterfaceId.Should().Be(newTargetId);
        result.TargetInterfaceName.Should().Be("Gi0/3");
        (await GetHandler(context).Handle(
                new GetConnectionQuery(created.Id), CancellationToken.None))
            .TargetInterfaceId.Should().Be(newTargetId);
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateConnectionCommand(unknownId, sourceId, targetId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Conexão '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task Update_SelfLoop_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateConnectionCommand(created.Id, sourceId, sourceId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A conexão não pode ligar a interface a ela mesma.*");
    }

    [Fact]
    public async Task Update_DuplicateOtherPair_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetA = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var targetB = await SeedInterfaceAsync(context, "sw-03", "Gi0/3");
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetA), CancellationToken.None);
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetB), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateConnectionCommand(created.Id, sourceId, targetA),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma conexão entre essas interfaces.");
    }

    [Fact]
    public async Task Update_UnknownSource_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateConnectionCommand(created.Id, Guid.NewGuid(), targetId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Interface * não encontrado.");
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Valid_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");

        var result = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.SourceInterfaceId.Should().Be(sourceId);
        result.SourceInterfaceName.Should().Be("Gi0/1");
        result.TargetInterfaceId.Should().Be(targetId);
        result.TargetInterfaceName.Should().Be("Gi0/2");
        context.Connections.Should().ContainSingle(c => c.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownSource_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");

        var act = () => CreateHandler(context).Handle(
            new CreateConnectionCommand(unknownId, targetId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Create_UnknownTarget_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var unknownId = Guid.NewGuid();

        var act = () => CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Create_Duplicate_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        var act = () => CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma conexão entre essas interfaces.");
    }

    [Fact]
    public async Task Create_ReversePair_ShouldSucceed_Directed()
    {
        await using var context = await CreateContextAsync();
        var a = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var b = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(a, b), CancellationToken.None);

        var act = () => CreateHandler(context).Handle(
            new CreateConnectionCommand(b, a), CancellationToken.None);

        await act.Should().NotThrowAsync(
            "par dirigido: (a,b) não bloqueia (b,a)");
        context.Connections.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_SelfLoop_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");

        var act = () => CreateHandler(context).Handle(
            new CreateConnectionCommand(interfaceId, interfaceId), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A conexão não pode ligar a interface a ela mesma.*");
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Existing_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteConnectionCommand(created.Id), CancellationToken.None);

        context.Connections.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => DeleteHandler(context).Handle(
            new DeleteConnectionCommand(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Conexão '{unknownId}' não encontrada.");
    }

    // ── Get ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        var created = await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        var result = await GetHandler(context).Handle(
            new GetConnectionQuery(created.Id), CancellationToken.None);

        result.Id.Should().Be(created.Id);
        result.SourceDeviceId.Should().NotBe(Guid.Empty);
        result.TargetDeviceId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => GetHandler(context).Handle(
            new GetConnectionQuery(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Conexão '{unknownId}' não encontrada.");
    }

    // ── Search ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByDeviceOnEitherSide()
    {
        await using var context = await CreateContextAsync();
        var sw1Nic1 = await SeedInterfaceAsync(context, "sw-01", "Gi0/1");
        var sw1Nic2 = await SeedInterfaceAsync(context, "sw-01", "Gi0/2");
        var sw2Nic = await SeedInterfaceAsync(context, "sw-02", "Gi0/1");
        var sw3Nic = await SeedInterfaceAsync(context, "sw-03", "Gi0/1");
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sw1Nic1, sw2Nic), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sw1Nic2, sw3Nic), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sw2Nic, sw3Nic), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sw3Nic, sw1Nic1), CancellationToken.None);

        var sw1Device = (await context.Devices.SingleAsync(d => d.Name == "sw-01")).Id;
        var scoped = await SearchHandler(context).Handle(
            new SearchConnectionsQuery(DeviceId: sw1Device), CancellationToken.None);

        scoped.TotalCount.Should().Be(
            3, "sw-01 participa de 3 conexões (2 na origem, 1 no destino)");
    }

    [Fact]
    public async Task Search_ShouldFilterByTermOnInterfaceNames()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Uplink-A");
        var targetId = await SeedInterfaceAsync(context, "sw-02", "Gi0/2");
        await CreateHandler(context).Handle(
            new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);

        var bySource = await SearchHandler(context).Handle(
            new SearchConnectionsQuery(Term: "uplink"), CancellationToken.None);
        var byTarget = await SearchHandler(context).Handle(
            new SearchConnectionsQuery(Term: "gi0/2"), CancellationToken.None);

        bySource.TotalCount.Should().Be(1, "termo casa na interface de origem (case-insensitive)");
        byTarget.TotalCount.Should().Be(1, "termo casa na interface de destino");
    }

    [Fact]
    public async Task Search_ShouldPageOrderedBySourceInterfaceName()
    {
        await using var context = await CreateContextAsync();
        var sourceId = await SeedInterfaceAsync(context, "sw-01", "Core");
        foreach (var (device, nic) in new[]
        {
            ("sw-09", "Zeta"), ("sw-02", "Alfa"), ("sw-03", "Mike")
        })
        {
            var targetId = await SeedInterfaceAsync(context, device, nic);
            await CreateHandler(context).Handle(
                new CreateConnectionCommand(sourceId, targetId), CancellationToken.None);
        }

        var page1 = await SearchHandler(context).Handle(
            new SearchConnectionsQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await SearchHandler(context).Handle(
            new SearchConnectionsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(c => c.TargetInterfaceName).Should().Equal("Alfa", "Mike");
        page2.Items.Select(c => c.TargetInterfaceName).Should().Equal("Zeta");
    }
}
