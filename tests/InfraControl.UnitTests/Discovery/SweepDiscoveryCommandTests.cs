using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// Network Explorer F4 — RunDiscoverySweepCommand: executa todos os
/// providers canônicos por device, um run por provider; falha de um
/// provider não aborta os demais.
/// </summary>
public sealed class SweepDiscoveryCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SweepDiscoveryCommandTests()
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

    private sealed class FakeProvider(string key, string name = "Fake") : IDiscoveryProvider
    {
        public string Name => name;
        public bool CanHandle(string providerKey)
            => string.Equals(providerKey, key, StringComparison.OrdinalIgnoreCase);
        public Task<IReadOnlyList<DiscoveryProviderResult>> DiscoverAsync(
            string target, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscoveryProviderResult>>([]);
    }

    private static RunDiscoverySweepCommandHandler Handler(
        AppDbContext context,
        IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto> startHandler)
        => new(
            new DeviceRepository(context),
            new ProviderRegistry<IDiscoveryProvider>(
                DiscoverySweepProviders.Keys.Select(k => new FakeProvider(k)).ToArray()),
            startHandler,
            NullLogger<RunDiscoverySweepCommandHandler>.Instance);

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context)
    {
        var location = Location.Create("DC");
        context.Locations.Add(location);
        var device = Device.Create(
            location.Id, "sw-sweep", DeviceType.Switch, ip: "192.0.2.10");
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    [Fact]
    public void Command_ShouldRequireOperatorOrAdmin()
        => new RunDiscoverySweepCommand(Guid.NewGuid())
            .RequiredRoles.Should()
            .BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task Sweep_ShouldRunEveryCanonicalProviderInOrder()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var calls = new List<string>();

        var startHandler = new Mock<IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto>>();
        startHandler
            .Setup(h => h.Handle(It.IsAny<StartDiscoveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StartDiscoveryCommand cmd, CancellationToken _) =>
            {
                calls.Add(cmd.Provider);
                return new DiscoveryRunDto(
                    Guid.NewGuid(), cmd.DeviceId, "Completed",
                    DateTime.UtcNow, DateTime.UtcNow, null);
            });

        var runs = await Handler(context, startHandler.Object)
            .Handle(new RunDiscoverySweepCommand(deviceId), CancellationToken.None);

        calls.Should().Equal(DiscoverySweepProviders.Keys);
        runs.Should().HaveCount(DiscoverySweepProviders.Keys.Count);
    }

    [Fact]
    public async Task Sweep_WhenOneProviderThrows_ShouldContinueWithTheRest()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var calls = new List<string>();

        var startHandler = new Mock<IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto>>();
        startHandler
            .Setup(h => h.Handle(It.IsAny<StartDiscoveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StartDiscoveryCommand cmd, CancellationToken _) =>
            {
                calls.Add(cmd.Provider);
                if (cmd.Provider == "Snmp")
                    throw new InvalidOperationException("provider indisponível");
                return new DiscoveryRunDto(
                    Guid.NewGuid(), cmd.DeviceId, "Completed",
                    DateTime.UtcNow, DateTime.UtcNow, null);
            });

        var runs = await Handler(context, startHandler.Object)
            .Handle(new RunDiscoverySweepCommand(deviceId), CancellationToken.None);

        calls.Should().HaveCount(DiscoverySweepProviders.Keys.Count);
        runs.Should().HaveCount(DiscoverySweepProviders.Keys.Count - 1);
    }

    [Fact]
    public async Task Sweep_UnknownDevice_ShouldThrowNotFound()
    {
        await using var context = await CreateContextAsync();
        var startHandler = new Mock<IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto>>();

        var act = () => Handler(context, startHandler.Object)
            .Handle(new RunDiscoverySweepCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        startHandler.Verify(
            h => h.Handle(It.IsAny<StartDiscoveryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
