using FluentAssertions;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T001: queries do módulo Network reusam o fluxo de Device (F2-T003) —
/// SearchSwitchesQuery fixa type=Switch delegando ao SearchDevicesQueryHandler,
/// GetSwitchQuery guarda DeviceType.Switch → 404 pt-BR. Sem entidade nova
/// (docs/03: Switch(Device) 1:N SwitchPort), sem migração.
/// </summary>
public sealed class SwitchQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SwitchQueryTests()
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

    private static SearchSwitchesQueryHandler SearchHandler(AppDbContext context)
        => new(new SearchDevicesQueryHandler(new DeviceRepository(context)));

    private static GetSwitchQueryHandler GetHandler(AppDbContext context)
        => new(new DeviceRepository(context));

    private static async Task<(Guid LocationA, Guid LocationB)> SeedDevicesAsync(AppDbContext context)
    {
        var locationA = Location.Create("Data Center A");
        var locationB = Location.Create("Data Center B");
        context.Locations.AddRange(locationA, locationB);
        await context.SaveChangesAsync();

        context.Devices.AddRange(
            Device.Create(locationA.Id, "sw-core", DeviceType.Switch, ip: "10.0.0.1"),
            Device.Create(locationA.Id, "sw-access", DeviceType.Switch),
            Device.Create(locationA.Id, "pr-01", DeviceType.Printer),
            Device.Create(locationA.Id, "srv-01", DeviceType.Server),
            Device.Create(locationB.Id, "sw-branch", DeviceType.Switch));
        await context.SaveChangesAsync();

        return (locationA.Id, locationB.Id);
    }

    [Fact]
    public async Task Search_ShouldReturnOnlySwitches()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchSwitchesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(item => item.Type == "Switch",
            "a busca de switches deve fixar type=Switch e ignorar demais tipos");
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(
            ["sw-core", "sw-access", "sw-branch"]);
    }

    [Fact]
    public async Task Search_ShouldApplyTermTogetherWithType()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchSwitchesQuery(Term: "core"), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("sw-core");
    }

    [Fact]
    public async Task Search_ShouldFilterByLocation()
    {
        await using var context = await CreateContextAsync();
        var (locationA, _) = await SeedDevicesAsync(context);

        var result = await SearchHandler(context).Handle(
            new SearchSwitchesQuery(LocationId: locationA), CancellationToken.None);

        result.TotalCount.Should().Be(2, "só os dois switches do location A");
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(["sw-core", "sw-access"]);
    }

    [Fact]
    public async Task Get_ExistingSwitch_ShouldReturnDeviceDto()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);
        var switchId = context.Devices.Single(device => device.Name == "sw-core").Id;

        var result = await GetHandler(context).Handle(
            new GetSwitchQuery(switchId), CancellationToken.None);

        result.Id.Should().Be(switchId);
        result.Name.Should().Be("sw-core");
        result.Type.Should().Be(nameof(DeviceType.Switch));
        result.Ip.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task Get_PrinterDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        await SeedDevicesAsync(context);
        var printerId = context.Devices.Single(device => device.Name == "pr-01").Id;

        var action = () => GetHandler(context).Handle(
            new GetSwitchQuery(printerId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Switch '{printerId}' não encontrado.",
                "device que não é switch não pode ser exposto pelo endpoint de switches");
    }

    [Fact]
    public async Task Get_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => GetHandler(context).Handle(
            new GetSwitchQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Switch '{unknownId}' não encontrado.");
    }
}
