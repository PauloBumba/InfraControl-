using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.IpAddresses;
using InfraControl.Application.Inventory.IpAddresses.Commands;
using InfraControl.Application.Inventory.IpAddresses.Queries;
using InfraControl.Application.Inventory.NetworkInterfaces;
using InfraControl.Application.Inventory.NetworkInterfaces.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T006: handlers do slice IpAddress com repositório real em Sqlite.
/// Mapeamento no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409. IP único GLOBAL (normalização canônica).
/// </summary>
public sealed class IpAddressCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public IpAddressCommandTests()
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
        string deviceName = "sw-01",
        string interfaceName = "eth0")
    {
        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        var device = Device.Create(location.Id, deviceName, DeviceType.Switch);
        context.Devices.Add(device);
        var nic = NetworkInterface.Create(device.Id, interfaceName);
        context.NetworkInterfaces.Add(nic);
        await context.SaveChangesAsync();
        return nic.Id;
    }

    private static CreateIpAddressCommandHandler CreateHandler(AppDbContext context)
        => new(
            new IpAddressRepository(context),
            new NetworkInterfaceRepository(context),
            NullLogger<CreateIpAddressCommandHandler>.Instance);

    private static UpdateIpAddressCommandHandler UpdateHandler(AppDbContext context)
        => new(new IpAddressRepository(context), NullLogger<UpdateIpAddressCommandHandler>.Instance);

    private static DeleteIpAddressCommandHandler DeleteHandler(AppDbContext context)
        => new(new IpAddressRepository(context), NullLogger<DeleteIpAddressCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistCanonicalIpAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "  192.0.2.10  "),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.InterfaceId.Should().Be(interfaceId);
        result.Ip.Should().Be("192.0.2.10");
        context.IpAddresses.Should().ContainSingle(entity => entity.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownInterface_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownInterface = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateIpAddressCommand(unknownInterface, "192.0.2.10"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownInterface}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateIp_DifferentIpv6Format_ShouldThrowConflictCanonical()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "2001:db8::1"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "2001:0DB8:0000::1"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o IP '2001:db8::1'.");
    }

    [Fact]
    public async Task Create_SameIpOnAnotherInterface_ShouldThrowConflictGlobal()
    {
        await using var context = await CreateContextAsync();
        var firstInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(firstInterface, "192.0.2.10"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateIpAddressCommand(secondInterface, "192.0.2.10"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o IP '192.0.2.10'.",
                "a unicidade do IP é global, não por interface");
    }

    [Fact]
    public async Task Create_InvalidIp_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O IP informado é inválido.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingRecord_ShouldChangeIp()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateIpAddressCommand(created.Id, "198.51.100.7"), CancellationToken.None);

        var stored = await context.IpAddresses.SingleAsync(entity => entity.Id == created.Id);
        stored.Ip.Should().Be("198.51.100.7");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateIpAddressCommand(unknownId, "192.0.2.10"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"IP '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToIpOfAnotherRecord_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "198.51.100.7"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateIpAddressCommand(second.Id, "192.0.2.10"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o IP '192.0.2.10'.");
    }

    [Fact]
    public async Task Update_ToItsOwnIp_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateIpAddressCommand(created.Id, "192.0.2.10"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingRecord_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteIpAddressCommand(created.Id), CancellationToken.None);

        context.IpAddresses.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteIpAddressCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"IP '{unknownId}' não encontrado.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByInterface()
    {
        await using var context = await CreateContextAsync();
        var firstInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(firstInterface, "10.0.0.1"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(firstInterface, "2001:db8::1"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(secondInterface, "10.0.1.1"), CancellationToken.None);

        var handler = new SearchIpAddressesQueryHandler(new IpAddressRepository(context));
        var scoped = await handler.Handle(
            new SearchIpAddressesQuery(InterfaceId: firstInterface), CancellationToken.None);
        var all = await handler.Handle(new SearchIpAddressesQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(2, "o filtro por interface deve restringir os IPs");
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ShouldFilterByDevice()
    {
        await using var context = await CreateContextAsync();
        var firstInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        var firstDeviceId = (await context.NetworkInterfaces
            .SingleAsync(nic => nic.Id == firstInterface)).DeviceId;
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(firstInterface, "10.0.0.1"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(secondInterface, "10.0.1.1"), CancellationToken.None);

        var handler = new SearchIpAddressesQueryHandler(new IpAddressRepository(context));
        var scoped = await handler.Handle(
            new SearchIpAddressesQuery(DeviceId: firstDeviceId), CancellationToken.None);
        var all = await handler.Handle(new SearchIpAddressesQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(1, "o filtro por device deve varrer as interfaces do device");
        all.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_ShouldFilterByTermIgnoringCase()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "2001:db8::1"), CancellationToken.None);

        var handler = new SearchIpAddressesQueryHandler(new IpAddressRepository(context));
        var byV4 = await handler.Handle(
            new SearchIpAddressesQuery(Term: "192.0.2"), CancellationToken.None);
        var byV6 = await handler.Handle(
            new SearchIpAddressesQuery(Term: "2001:DB8"), CancellationToken.None);

        byV4.TotalCount.Should().Be(1, "termo IPv4 deve casar por substring");
        byV6.TotalCount.Should().Be(1, "termo IPv6 deve casar ignorando maiúsculas");
    }

    [Fact]
    public async Task Search_ShouldPageOrderedByIp()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        foreach (var value in new[] { "10.0.0.2", "10.0.0.1", "10.0.0.3" })
            await CreateHandler(context).Handle(
                new CreateIpAddressCommand(interfaceId, value), CancellationToken.None);

        var handler = new SearchIpAddressesQueryHandler(new IpAddressRepository(context));
        var page1 = await handler.Handle(
            new SearchIpAddressesQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchIpAddressesQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(item => item.Ip).Should().Equal("10.0.0.1", "10.0.0.2");
        page2.Items.Select(item => item.Ip).Should().Equal("10.0.0.3");
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingRecord_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateIpAddressCommand(interfaceId, "192.0.2.10"), CancellationToken.None);

        var handler = new GetIpAddressQueryHandler(new IpAddressRepository(context));
        var result = await handler.Handle(new GetIpAddressQuery(created.Id), CancellationToken.None);

        result.Should().Be(new IpAddressDto(created.Id, interfaceId, "192.0.2.10"));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetIpAddressQueryHandler(new IpAddressRepository(context));
        var action = () => handler.Handle(new GetIpAddressQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"IP '{unknownId}' não encontrado.");
    }
}
