using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Moq;
using Xunit;

namespace InfraControl.UnitTests.Integrations;

/// <summary>F26 Fase 8: dashboard separa persistido × ao vivo; erro ≠ zero.</summary>
public sealed class UniFiDashboardQueryTests
{
    private readonly Mock<IUniFiSitesRepository> _sites = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IExternalDeviceReferenceRepository> _references = new();
    private readonly Mock<IUnifiApiClient> _unifi = new();
    private readonly Mock<IAuditReader> _audit = new();
    private readonly Mock<ISender> _sender = new();

    private GetUnifiDashboardQueryHandler CreateHandler() => new(
        _sites.Object, _devices.Object, _references.Object,
        _unifi.Object, _audit.Object, _sender.Object);

    private void ArrangeBaseline()
    {
        _sites.Setup(s => s.ListAsync(default)).ReturnsAsync(new List<UniFiSite>
        {
            UniFiSite.Create("default", "Default", "", false),
        });
        _devices.Setup(d => d.GetAllOrderedByNameAsync(default))
            .ReturnsAsync(new List<Device>
            {
                Device.Create(Guid.NewGuid(), "sw-01", DeviceType.Switch),
                Device.Create(Guid.NewGuid(), "sw-02", DeviceType.Switch),
                Device.Create(Guid.NewGuid(), "ap-01", DeviceType.AccessPoint),
            });
        _sender.Setup(s => s.Send(It.IsAny<GetUniFiCorrelationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UniFiCorrelationDto(
                new List<UniFiAssetLinkDto>(), new List<UniFiUnlinkedDeviceDto>(), new List<UniFiAssetLinkDto>()));
        _audit.Setup(a => a.SearchAsync("SyncUnifiInventoryCommand", "Succeeded",
                null, null, 1, 1, default))
            .ReturnsAsync(new PagedResult<AuditEntrySummary>(
                new List<AuditEntrySummary>(), 0, 1, 1));
        _audit.Setup(a => a.SearchAsync("SyncUnifiInventoryCommand", "Failed",
                null, null, 1, 1, default))
            .ReturnsAsync(new PagedResult<AuditEntrySummary>(
                new List<AuditEntrySummary>(), 0, 1, 1));
        _audit.Setup(a => a.SearchAsync("SyncUnifiInventoryCommand", "Failed",
                It.IsAny<DateTime?>(), null, 1, 100, default))
            .ReturnsAsync(new PagedResult<AuditEntrySummary>(
                new List<AuditEntrySummary>(), 0, 1, 1));
        _unifi.Setup(u => u.ListDevicesAsync(default))
            .ReturnsAsync(new List<UnifiDeviceDto>
            {
                new("aa:bb:cc:00:00:01", "ap-01", "U7PG2", "uap", "10.0.0.1", State: "1"),
                new("aa:bb:cc:00:00:02", "sw-01", "USW", "usw", "10.0.0.2", State: "1"),
                new("aa:bb:cc:00:00:03", "sw-02", "USW", "usw", "10.0.0.3", State: "0"),
            });
        _unifi.Setup(u => u.ListConnectedClientsAsync(default))
            .ReturnsAsync(new List<UnifiConnectedClientDto>());
        _unifi.Setup(u => u.GetPortsSummaryAsync(default))
            .ReturnsAsync(new UnifiPortsSummaryDto(48, 30));
    }

    [Fact]
    public async Task Dashboard_Combina_Persistido_E_AoVivo()
    {
        ArrangeBaseline();

        var result = await CreateHandler().Handle(new GetUnifiDashboardQuery(), default);

        result.Sites.Should().Be(1);
        result.Switches.Should().Be(2);
        result.AccessPoints.Should().Be(1);
        result.DeviceStates["1"].Should().Be(2);
        result.DeviceStates["0"].Should().Be(1);
        result.Ports.Should().Be(new UnifiPortsSummaryDto(48, 30));
        result.ControllerReachable.Should().BeTrue();
        result.ControllerError.Should().BeNull();
        result.Stale.Should().BeTrue(); // nunca sincronizou
        result.LastSyncAtUtc.Should().BeNull();
        result.LastSyncSucceeded.Should().BeNull();
    }

    [Fact]
    public async Task Dashboard_ControllerIndisponivel_NaoConfundeErroComZero()
    {
        ArrangeBaseline();
        _unifi.Setup(u => u.ListDevicesAsync(default))
            .ThrowsAsync(new HttpRequestException("controller fora do ar"));

        var result = await CreateHandler().Handle(new GetUnifiDashboardQuery(), default);

        result.ControllerReachable.Should().BeFalse();
        result.ControllerError.Should().Contain("fora do ar");
        result.DeviceStates.Should().BeEmpty();
        result.Ports.Should().BeNull();
        result.Clients.Should().Be(0);
        // dados persistidos continuam respondendo mesmo sem controller:
        result.Switches.Should().Be(2);
        result.Sites.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_UltimaSyncComFalha_MaisRecente_Sinaliza()
    {
        ArrangeBaseline();
        var now = DateTime.UtcNow;
        _audit.Setup(a => a.SearchAsync("SyncUnifiInventoryCommand", "Succeeded",
                null, null, 1, 1, default))
            .ReturnsAsync(new PagedResult<AuditEntrySummary>(new List<AuditEntrySummary>
            {
                new(Guid.NewGuid(), "SyncUnifiInventoryCommand", "UniFiSync", "Succeeded",
                    null, null, null, null, now.AddHours(-3)),
            }, 1, 1, 1));
        _audit.Setup(a => a.SearchAsync("SyncUnifiInventoryCommand", "Failed",
                null, null, 1, 1, default))
            .ReturnsAsync(new PagedResult<AuditEntrySummary>(new List<AuditEntrySummary>
            {
                new(Guid.NewGuid(), "SyncUnifiInventoryCommand", "UniFiSync", "Failed",
                    null, null, null, "boom", now.AddHours(-1)),
            }, 1, 1, 1));

        var result = await CreateHandler().Handle(new GetUnifiDashboardQuery(), default);

        result.LastSyncSucceeded.Should().BeFalse();
        result.LastSyncAtUtc.Should().BeCloseTo(now.AddHours(-1), TimeSpan.FromSeconds(5));
        result.Stale.Should().BeFalse();
    }
}
