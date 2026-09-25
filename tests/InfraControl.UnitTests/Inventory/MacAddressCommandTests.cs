using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.MacAddresses;
using InfraControl.Application.Inventory.MacAddresses.Commands;
using InfraControl.Application.Inventory.MacAddresses.Queries;
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
/// F2-T005: handlers do slice MacAddress com repositório real em Sqlite.
/// Mapeamento no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409. MAC único GLOBAL (case-insensitive).
/// </summary>
public sealed class MacAddressCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MacAddressCommandTests()
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

    private static CreateMacAddressCommandHandler CreateHandler(AppDbContext context)
        => new(
            new MacAddressRepository(context),
            new NetworkInterfaceRepository(context),
            NullLogger<CreateMacAddressCommandHandler>.Instance);

    private static UpdateMacAddressCommandHandler UpdateHandler(AppDbContext context)
        => new(new MacAddressRepository(context), NullLogger<UpdateMacAddressCommandHandler>.Instance);

    private static DeleteMacAddressCommandHandler DeleteHandler(AppDbContext context)
        => new(new MacAddressRepository(context), NullLogger<DeleteMacAddressCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistNormalizedMacAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "  AA-BB-CC-DD-EE-FF  "),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.InterfaceId.Should().Be(interfaceId);
        result.Mac.Should().Be("aa:bb:cc:dd:ee:ff");
        context.MacAddresses.Should().ContainSingle(mac => mac.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownInterface_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownInterface = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateMacAddressCommand(unknownInterface, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Interface '{unknownInterface}' não encontrado.");
    }

    [Fact]
    public async Task Create_DuplicateMac_IgnoringCaseAndFormat_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "AA-BB-CC-DD-EE-FF"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o MAC 'aa:bb:cc:dd:ee:ff'.");
    }

    [Fact]
    public async Task Create_SameMacOnAnotherInterface_ShouldThrowConflictGlobal()
    {
        await using var context = await CreateContextAsync();
        var firstInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(firstInterface, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateMacAddressCommand(secondInterface, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o MAC 'aa:bb:cc:dd:ee:ff'.",
                "a unicidade do MAC é global, não por interface");
    }

    [Fact]
    public async Task Create_InvalidMac_ShouldThrowArgumentException()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O MAC informado é inválido.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingRecord_ShouldChangeMac()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateMacAddressCommand(created.Id, "11-22-33-44-55-66"), CancellationToken.None);

        var stored = await context.MacAddresses.SingleAsync(mac => mac.Id == created.Id);
        stored.Mac.Should().Be("11:22:33:44:55:66");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateMacAddressCommand(unknownId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"MAC '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_ToMacOfAnotherRecord_ShouldThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "11:22:33:44:55:66"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateMacAddressCommand(second.Id, "AA-BB-CC-DD-EE-FF"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um cadastro com o MAC 'aa:bb:cc:dd:ee:ff'.");
    }

    [Fact]
    public async Task Update_ToItsOwnMac_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateMacAddressCommand(created.Id, "AA-BB-CC-DD-EE-FF"), CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingRecord_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteMacAddressCommand(created.Id), CancellationToken.None);

        context.MacAddresses.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteMacAddressCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"MAC '{unknownId}' não encontrado.");
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByInterface()
    {
        await using var context = await CreateContextAsync();
        var firstInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(firstInterface, "aa:bb:cc:dd:ee:01"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(firstInterface, "aa:bb:cc:dd:ee:02"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(secondInterface, "aa:bb:cc:dd:ee:03"), CancellationToken.None);

        var handler = new SearchMacAddressesQueryHandler(new MacAddressRepository(context));
        var scoped = await handler.Handle(
            new SearchMacAddressesQuery(InterfaceId: firstInterface), CancellationToken.None);
        var all = await handler.Handle(new SearchMacAddressesQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(2, "o filtro por interface deve restringir os MACs");
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ShouldFilterByDevice()
    {
        await using var context = await CreateContextAsync();
        var firstDeviceInterface = await SeedInterfaceAsync(context, "sw-01", "eth0");
        var secondDeviceInterface = await SeedInterfaceAsync(context, "sw-02", "eth0");
        var firstDeviceId = (await context.NetworkInterfaces
            .SingleAsync(nic => nic.Id == firstDeviceInterface)).DeviceId;
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(firstDeviceInterface, "aa:bb:cc:dd:ee:01"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(secondDeviceInterface, "aa:bb:cc:dd:ee:02"), CancellationToken.None);

        var handler = new SearchMacAddressesQueryHandler(new MacAddressRepository(context));
        var scoped = await handler.Handle(
            new SearchMacAddressesQuery(DeviceId: firstDeviceId), CancellationToken.None);
        var all = await handler.Handle(new SearchMacAddressesQuery(), CancellationToken.None);

        scoped.TotalCount.Should().Be(1, "o filtro por device deve varrer as interfaces do device");
        all.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_ShouldFilterByTermIgnoringSeparatorsAndCase()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "aa:bb:cc:dd:ee:ff"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "11:22:33:44:55:66"), CancellationToken.None);

        var handler = new SearchMacAddressesQueryHandler(new MacAddressRepository(context));
        var byTerm = await handler.Handle(
            new SearchMacAddressesQuery(Term: "AA-BB"), CancellationToken.None);
        var byOtherTerm = await handler.Handle(
            new SearchMacAddressesQuery(Term: "4455"), CancellationToken.None);

        byTerm.TotalCount.Should().Be(1, "termo deve casar ignorando separadores e maiúsculas");
        byOtherTerm.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_ShouldPageOrderedByMac()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        foreach (var value in new[] { "02:22:33:44:55:66", "01:22:33:44:55:66", "03:22:33:44:55:66" })
            await CreateHandler(context).Handle(
                new CreateMacAddressCommand(interfaceId, value), CancellationToken.None);

        var handler = new SearchMacAddressesQueryHandler(new MacAddressRepository(context));
        var page1 = await handler.Handle(
            new SearchMacAddressesQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchMacAddressesQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page1.TotalCount.Should().Be(3);
        page1.Items.Select(item => item.Mac).Should().Equal("01:22:33:44:55:66", "02:22:33:44:55:66");
        page2.Items.Select(item => item.Mac).Should().Equal("03:22:33:44:55:66");
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingRecord_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var interfaceId = await SeedInterfaceAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateMacAddressCommand(interfaceId, "AA-BB-CC-DD-EE-FF"), CancellationToken.None);

        var handler = new GetMacAddressQueryHandler(new MacAddressRepository(context));
        var result = await handler.Handle(new GetMacAddressQuery(created.Id), CancellationToken.None);

        result.Should().Be(new MacAddressDto(created.Id, interfaceId, "aa:bb:cc:dd:ee:ff"));
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetMacAddressQueryHandler(new MacAddressRepository(context));
        var action = () => handler.Handle(new GetMacAddressQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"MAC '{unknownId}' não encontrado.");
    }
}
