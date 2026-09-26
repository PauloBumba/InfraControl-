using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces;
using InfraControl.Application.Inventory.NetworkInterfaces.Commands;
using InfraControl.Application.Inventory.NetworkInterfaces.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T004: handlers do slice NetworkInterface com repositório real em Sqlite.
/// Mapeamento no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409. Nome único POR DEVICE (case-insensitive).
/// </summary>
public sealed class NetworkInterfaceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NetworkInterfaceCommandTests()
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

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context, string deviceName = "sw-01")
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        var device = Device.Create(location.Id, deviceName, DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static CreateNetworkInterfaceCommandHandler CreateHandler(AppDbContext context)
        => new(
            new NetworkInterfaceRepository(context),
            new DeviceRepository(context),
            NullLogger<CreateNetworkInterfaceCommandHandler>.Instance);

    private static UpdateNetworkInterfaceCommandHandler UpdateHandler(AppDbContext context)
        => new(new NetworkInterfaceRepository(context), NullLogger<UpdateNetworkInterfaceCommandHandler>.Instance);

    private static DeleteNetworkInterfaceCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new NetworkInterfaceRepository(context),
            new ConnectionRepository(context),
            NullLogger<DeleteNetworkInterfaceCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "  eth0  ", "Uplink"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.DeviceId.Should().Be(deviceId);
        result.Name.Should().Be("eth0");
        result.Description.Should().Be("Uplink");
        context.NetworkInterfaces.Should().ContainSingle(nic => nic.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownDevice = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(unknownDevice, "eth0"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownDevice}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateNameInSameDevice_IgnoringCase_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "  ETH0  "), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma interface 'ETH0' neste device.");
    }

    [Fact]
    public async Task Create_SameNameInDifferentDevice_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var firstDevice = await SeedDeviceAsync(context, "sw-01");
        var secondDevice = await SeedDeviceAsync(context, "sw-02");
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(firstDevice, "eth0"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(secondDevice, "eth0"), CancellationToken.None);

        var dto = (await action.Should().NotThrowAsync()).Which;
        dto.DeviceId.Should().Be(secondDevice);
    }

    [Fact]
    public async Task Create_InvalidName_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "   "), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da interface é obrigatório.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingInterface_ShouldChangeNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateNetworkInterfaceCommand(created.Id, "eth1", "Downlink"),
            CancellationToken.None);

        var stored = await context.NetworkInterfaces.SingleAsync(nic => nic.Id == created.Id);
        stored.Name.Should().Be("eth1");
        stored.Description.Should().Be("Downlink");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateNetworkInterfaceCommand(unknownId, "eth0"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToNameOfAnotherInterfaceInSameDevice_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth1"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateNetworkInterfaceCommand(second.Id, "ETH0"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma interface 'ETH0' neste device.");
    }

    [Fact]
    public async Task Update_ToItsOwnName_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateNetworkInterfaceCommand(created.Id, "ETH0"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_ToNameUsedOnAnotherDevice_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var firstDevice = await SeedDeviceAsync(context, "sw-01");
        var secondDevice = await SeedDeviceAsync(context, "sw-02");
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(firstDevice, "eth0"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(secondDevice, "mgmt0"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateNetworkInterfaceCommand(second.Id, "eth0"), CancellationToken.None);

        await action.Should().NotThrowAsync(
            "uniqueness de nome de interface é por device, não global");
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingInterface_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteNetworkInterfaceCommand(created.Id), CancellationToken.None);

        context.NetworkInterfaces.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteNetworkInterfaceCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Delete_InterfaceWithMacs_ShouldThrowConflictMessage()
    {
        // F2-T005: fecha a cadeia de bloqueio Device → Interface → MAC.
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);
        context.MacAddresses.Add(MacAddress.Create(created.Id, "aa:bb:cc:dd:ee:ff"));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteNetworkInterfaceCommand(created.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a interface: existem MACs vinculados.");
    }

    [Fact]
    public async Task Delete_InterfaceWithIps_ShouldThrowConflictMessage()
    {
        // F2-T006: fecha a dependência anotada na F2-T004.
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);
        context.IpAddresses.Add(IpAddress.Create(created.Id, "192.0.2.10"));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteNetworkInterfaceCommand(created.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a interface: existem IPs vinculados.");
    }

    [Fact]
    public async Task Delete_InterfaceWithConnections_ShouldThrowConflictMessage()
    {
        // F3-T005: fecha a cadeia de bloqueio Interface → Connection
        // (FK Restrict no banco é o backstop; 404/409 vem do handler).
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0"), CancellationToken.None);
        var peer = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth1"), CancellationToken.None);
        context.Connections.Add(Connection.Create(created.Id, peer.Id));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteNetworkInterfaceCommand(created.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a interface: existem conexões vinculadas.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByDevice()
    {
        await using var context = await CreateContextAsync();
        var firstDevice = await SeedDeviceAsync(context, "sw-01");
        var secondDevice = await SeedDeviceAsync(context, "sw-02");
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(firstDevice, "eth0"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(firstDevice, "eth1"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(secondDevice, "eth0"), CancellationToken.None);

        var handler = new SearchNetworkInterfacesQueryHandler(new NetworkInterfaceRepository(context));
        var scoped = await handler.Handle(
            new SearchNetworkInterfacesQuery(DeviceId: firstDevice), CancellationToken.None);
        var all = await handler.Handle(new SearchNetworkInterfacesQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(2, "o filtro por device deve restringir as interfaces");
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ShouldFilterByNameAndDescription()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "Core A"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "Borda", "uplink do rack norte"),
            CancellationToken.None);

        var handler = new SearchNetworkInterfacesQueryHandler(new NetworkInterfaceRepository(context));
        var byName = await handler.Handle(
            new SearchNetworkInterfacesQuery(Term: "CORE"), CancellationToken.None);
        var byDescription = await handler.Handle(
            new SearchNetworkInterfacesQuery(Term: "uplink"), CancellationToken.None);

        byName.TotalCount.Should().Be(1, "termo deve casar no nome (case-insensitive) sem vazar p/ descrição");
        byDescription.TotalCount.Should().Be(1, "termo deve casar na descrição");
    }

    [Fact]
    public async Task Search_ShouldPageOrderedByNameWithinDevice()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        foreach (var name in new[] { "Zeta", "Alfa", "Mike" })
            await CreateHandler(context).Handle(
                new CreateNetworkInterfaceCommand(deviceId, name), CancellationToken.None);

        var handler = new SearchNetworkInterfacesQueryHandler(new NetworkInterfaceRepository(context));
        var page1 = await handler.Handle(
            new SearchNetworkInterfacesQuery(DeviceId: deviceId, Page: 1, PageSize: 2),
            CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchNetworkInterfacesQuery(DeviceId: deviceId, Page: 2, PageSize: 2),
            CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(item => item.Name).Should().Equal("Alfa", "Mike");
        page2.Items.Select(item => item.Name).Should().Equal("Zeta");
    }

    [Fact]
    public async Task GetById_ExistingInterface_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateNetworkInterfaceCommand(deviceId, "eth0", "uplink"), CancellationToken.None);

        var handler = new GetNetworkInterfaceQueryHandler(new NetworkInterfaceRepository(context));
        var result = await handler.Handle(new GetNetworkInterfaceQuery(created.Id), CancellationToken.None);

        result.Should().Be(new NetworkInterfaceDto(created.Id, deviceId, "eth0", "uplink"));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetNetworkInterfaceQueryHandler(new NetworkInterfaceRepository(context));
        var action = () => handler.Handle(new GetNetworkInterfaceQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownId}' não encontrado.");
    }
}
