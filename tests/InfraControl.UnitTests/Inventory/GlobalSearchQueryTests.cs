using FluentAssertions;
using InfraControl.Application.Inventory.GlobalSearch;
using InfraControl.Application.Inventory.GlobalSearch.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T007: busca global do inventário — agrega as 6 fontes (Location, Rack,
/// Device, NetworkInterface, MacAddress, IpAddress) em um único resultado
/// tipado, somente leitura, reusando os SearchAsync já existentes.
/// </summary>
public sealed class GlobalSearchQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GlobalSearchQueryTests()
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

    private static GlobalSearchQueryHandler CreateHandler(AppDbContext context) => new(
        new LocationRepository(context),
        new RackRepository(context),
        new DeviceRepository(context),
        new NetworkInterfaceRepository(context),
        new MacAddressRepository(context),
        new IpAddressRepository(context));

    private static async Task<(Location Location, Rack Rack, Device Device,
        NetworkInterface Nic, MacAddress Mac, IpAddress Ip)> SeedAllSixAsync(AppDbContext context)
    {
        var location = Location.Create("Sala 0");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var rack = Rack.Create(location.Id, "Rack 0");
        context.Racks.Add(rack);
        var device = Device.Create(location.Id, "sw-00", DeviceType.Switch, ip: "10.0.0.1");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var nic = NetworkInterface.Create(device.Id, "eth0");
        context.NetworkInterfaces.Add(nic);
        await context.SaveChangesAsync();

        var mac = MacAddress.Create(nic.Id, "aa:00:bb:cc:dd:ee");
        context.MacAddresses.Add(mac);
        var ip = IpAddress.Create(nic.Id, "10.0.0.1");
        context.IpAddresses.Add(ip);
        await context.SaveChangesAsync();

        return (location, rack, device, nic, mac, ip);
    }

    [Fact]
    public async Task Search_ShouldAggregateAllSixTypes()
    {
        await using var context = await CreateContextAsync();
        var seeded = await SeedAllSixAsync(context);

        var result = await CreateHandler(context).Handle(
            new GlobalSearchQuery("0"), CancellationToken.None);

        result.TotalCount.Should().Be(6, "cada fonte deve contribuir com 1 resultado");
        result.Items.Should().HaveCount(6);
        result.Items.Select(item => item.Type).Should().BeEquivalentTo(
            ["location", "rack", "device", "interface", "mac", "ip"]);
        result.Items.Should().Contain(item =>
            item.Type == "location" && item.Id == seeded.Location.Id && item.Name == "Sala 0");
        result.Items.Should().Contain(item =>
            item.Type == "rack" && item.Id == seeded.Rack.Id && item.Name == "Rack 0");
        result.Items.Should().Contain(item =>
            item.Type == "device" && item.Id == seeded.Device.Id &&
            item.Name == "sw-00" && item.Subtitle == "10.0.0.1");
        result.Items.Should().Contain(item =>
            item.Type == "interface" && item.Id == seeded.Nic.Id && item.Name == "eth0");
        result.Items.Should().Contain(item =>
            item.Type == "mac" && item.Id == seeded.Mac.Id && item.Name == "aa:00:bb:cc:dd:ee");
        result.Items.Should().Contain(item =>
            item.Type == "ip" && item.Id == seeded.Ip.Id && item.Name == "10.0.0.1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_EmptyTerm_ShouldThrowPtBrMessage(string term)
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new GlobalSearchQuery(term), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O termo de busca é obrigatório.*");
    }

    [Fact]
    public async Task Search_UnknownTerm_ShouldReturnEmptyResult()
    {
        await using var context = await CreateContextAsync();
        await SeedAllSixAsync(context);

        var result = await CreateHandler(context).Handle(
            new GlobalSearchQuery("nao-existe-xyz"), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Search_ShouldCapResultsPerTypeButReportFullTotalCount()
    {
        await using var context = await CreateContextAsync();
        context.Locations.AddRange(
            Location.Create("Loc-A0"),
            Location.Create("Loc-B0"),
            Location.Create("Loc-C0"));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new GlobalSearchQuery("0", Limit: 2), CancellationToken.None);

        result.Items.Where(item => item.Type == "location").Should().HaveCount(2,
            "o limit deve valer por fonte, não no total");
        result.TotalCount.Should().Be(3, "o total deve refletir a contagem real das fontes");
    }

    [Fact]
    public async Task Search_ShouldTrimTerm()
    {
        await using var context = await CreateContextAsync();
        context.Locations.Add(Location.Create("Sala 0"));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new GlobalSearchQuery("  Sala 0  "), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("Sala 0");
    }

    [Fact]
    public async Task Search_ZeroLimit_ShouldClampToAtLeastOne()
    {
        await using var context = await CreateContextAsync();
        context.Locations.Add(Location.Create("Loc-0"));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new GlobalSearchQuery("0", Limit: 0), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
    }
}
