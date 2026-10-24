using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F33-T005/ADR-059 (spike: fonte da série = API history do Zabbix):
/// a fábrica resolve Device → host → item → série de 2h e degrada para
/// null em QUALQUER passo sem dado (nunca lança — o envio segue em texto).
/// </summary>
public sealed class ZabbixWhatsAppChartFactoryTests
{
    private static readonly DateTime EventAt = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TryCreateAsync_WithoutDeviceId_ShouldReturnNull()
    {
        var factory = CreateFactory(out _, out _, device: null);

        var chart = await factory.TryCreateAsync(
            deviceId: null, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_WhenZabbixNotConfigured_ShouldReturnNull()
    {
        var device = CreateDevice();
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.IsConfigured = false;

        var chart = await factory.TryCreateAsync(
            device.Id, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_WhenDeviceNotFound_ShouldReturnNull()
    {
        var factory = CreateFactory(out _, out _, device: null);

        var chart = await factory.TryCreateAsync(
            Guid.NewGuid(), "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_WhenHostNotFound_ShouldReturnNull()
    {
        var device = CreateDevice(name: "sw-sem-zabbix");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "outro-switch"));

        var chart = await factory.TryCreateAsync(
            device.Id, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_WhenNoEnabledItem_ShouldReturnNull()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "Desativado", "system.cpu",
                Type: 0, Enabled: false, Unsupported: false),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_WhenHistoryEmpty_ShouldReturnNull()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "CPU", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull("série vazia = cartão sem valor → degrada para texto");
    }

    [Fact]
    public async Task TryCreateAsync_HappyPath_ShouldBuildSortedCpuSeries()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("200", "10", "Ping", "icmpping",
                Type: 0, Enabled: true, Unsupported: false),
            new ZabbixItemDto("100", "10", "CPU utilization", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false, Unit: "%"),
        ];
        zabbix.History["100"] =
        [
            new ZabbixHistoryValueDto("100", EventAt.AddMinutes(-10), "98.5"),
            new ZabbixHistoryValueDto("100", EventAt.AddMinutes(-30), "92"),
            new ZabbixHistoryValueDto("100", EventAt, "99"),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id,
            "Alerta Critical criado",
            "O alerta 'CPU utilization acima do limite' foi aberto (Critical).",
            "IncidentCreatedDomainEvent",
            EventAt);

        chart.Should().NotBeNull();
        chart!.Kind.Should().Be(ChartCardKind.Incident);
        chart.Title.Should().Be("CPU utilization acima do limite");
        chart.DeviceLabel.Should().Be("sw-01");
        chart.SeverityLabel.Should().Be("Critical");
        chart.Unit.Should().Be("%");
        chart.Series.Should().HaveCount(3);
        chart.Series.Select(point => point.Value)
            .Should().Equal(92d, 98.5, 99d);
        chart.CurrentValue.Should().Be(99);
        chart.MinValue.Should().Be(92);
        chart.MaxValue.Should().Be(99);
    }

    [Fact]
    public async Task TryCreateAsync_OnZabbixError_ShouldReturnNullWithoutThrowing()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.HostsThrow = true;

        var act = () => factory.TryCreateAsync(
            device.Id, "Título", "", "IncidentCreatedDomainEvent", EventAt);

        (await act()).Should().BeNull("erro do Zabbix nunca pode travar o envio");
    }

    [Theory]
    [InlineData("IncidentCreatedDomainEvent", ChartCardKind.Incident)]
    [InlineData("AlertCreatedDomainEvent", ChartCardKind.Incident)]
    [InlineData("IncidentResolvedDomainEvent", ChartCardKind.Resolution)]
    [InlineData("AlertResolvedDomainEvent", ChartCardKind.Resolution)]
    public async Task TryCreateAsync_KindMapping_ShouldFollowEventType(
        string eventType, ChartCardKind expected)
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "CPU", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false),
        ];
        zabbix.History["100"] =
        [
            new ZabbixHistoryValueDto("100", EventAt.AddMinutes(-5), "42"),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "CPU", "", eventType, EventAt);

        chart!.Kind.Should().Be(expected);
    }

    [Fact]
    public async Task TryCreateAsync_WhenNoItemMatchesAlert_ShouldReturnNull()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "CPU utilization", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "Alerta de disco cheio", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull("um gráfico de CPU seria enganoso para alerta de disco");
    }

    [Fact]
    public async Task TryCreateAsync_WhenItemMatchIsAmbiguous_ShouldReturnNull()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "CPU utilization", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false),
            new ZabbixItemDto("101", "10", "CPU utilization", "cpu.util.other",
                Type: 0, Enabled: true, Unsupported: false),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "CPU utilization high", "", "IncidentCreatedDomainEvent", EventAt);

        chart.Should().BeNull("não há informação suficiente para escolher entre séries equivalentes");
    }

    [Fact]
    public async Task TryCreateAsync_WarningSeverity_ShouldUseWarningCard()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "toner level", "printer.toner",
                Type: 0, Enabled: true, Unsupported: false, Unit: "%"),
        ];
        zabbix.History["100"] =
        [
            new ZabbixHistoryValueDto("100", EventAt, "18"),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id, "Alerta Warning criado", "O alerta 'Toner baixo' foi aberto (Warning).",
            "AlertCreatedDomainEvent", EventAt);

        chart!.Kind.Should().Be(ChartCardKind.Warning);
    }

    [Fact]
    public async Task TryCreateAsync_ResolvedAlert_ShouldExtractTitleAndMatchItem()
    {
        var device = CreateDevice(name: "sw-01");
        var factory = CreateFactory(out _, out var zabbix, device);
        zabbix.Hosts.Add(Host(hostId: "10", name: "sw-01"));
        zabbix.Items["10"] =
        [
            new ZabbixItemDto("100", "10", "CPU utilization", "system.cpu.util",
                Type: 0, Enabled: true, Unsupported: false, Unit: "%"),
        ];
        zabbix.History["100"] =
        [
            new ZabbixHistoryValueDto("100", EventAt, "42"),
        ];

        var chart = await factory.TryCreateAsync(
            device.Id,
            "Alerta resolvido",
            "O alerta 'CPU utilization acima do limite' foi resolvido.",
            "AlertResolvedDomainEvent",
            EventAt);

        chart.Should().NotBeNull();
        chart!.Title.Should().Be("CPU utilization acima do limite");
        chart.Kind.Should().Be(ChartCardKind.Resolution);
        chart.Unit.Should().Be("%");
    }

    private static Device CreateDevice(string name = "sw-01", string? ip = null)
        => Device.Create(Guid.NewGuid(), name, DeviceType.Switch, ip: ip);

    private static ZabbixHostDto Host(string hostId, string name, string? ip = null)
        => new(hostId, Host: name.ToLowerInvariant(), Name: name, Enabled: true,
            Discovered: false,
            Interfaces:
            [
                new ZabbixInterfaceDto(ip ?? "10.0.0.1", Port: 10050, Type: 1, Main: true),
            ],
            GroupIds: [], TemplateIds: []);

    private static ZabbixWhatsAppChartFactory CreateFactory(
        out FakeDeviceRepository devices,
        out FakeZabbixClient zabbix,
        Device? device)
    {
        devices = new FakeDeviceRepository(device);
        zabbix = new FakeZabbixClient();
        return new ZabbixWhatsAppChartFactory(
            devices, zabbix, NullLogger<ZabbixWhatsAppChartFactory>.Instance);
    }

    private sealed class FakeDeviceRepository(Device? device) : IDeviceRepository
    {
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(device is not null && device.Id == id ? device : null);

        public Task<Device?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Device?> GetByIpAsync(string ip, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Device?> GetByMacAsync(string mac, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<Device>> SearchAsync(
            Guid? locationId, Guid? rackId, DeviceType? type, string? term,
            int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasByRackAsync(Guid rackId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasInterfacesAsync(Guid deviceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasPortsAsync(Guid deviceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasTasksAsync(Guid deviceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasCameraNvrMappingsAsync(
            Guid nvrId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> HasLinkedVirtualMachinesAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<Device>> SearchVirtualMachinesByLinkAsync(
            Guid? hypervisorId, Guid? vCenterId, string? term,
            int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Device>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AddAsync(Device entity, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Remove(Device entity) => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeZabbixClient : IZabbixClient
    {
        public bool IsConfigured { get; set; } = true;
        public bool HostsThrow { get; set; }
        public List<ZabbixHostDto> Hosts { get; } = [];
        public Dictionary<string, IReadOnlyList<ZabbixItemDto>> Items { get; } = new();
        public Dictionary<string, IReadOnlyList<ZabbixHistoryValueDto>> History { get; } = new();

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("8.0.0");

        public Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(
            CancellationToken cancellationToken = default)
            => HostsThrow
                ? throw new HttpRequestException("zabbix offline")
                : Task.FromResult<IReadOnlyList<ZabbixHostDto>>(Hosts);

        public Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixHostGroupDto>>([]);

        public Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixTemplateDto>>([]);

        public Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
            string hostId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                Items.TryGetValue(hostId, out var items)
                    ? items
                    : (IReadOnlyList<ZabbixItemDto>)[]);

        public Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixTriggerDto>>([]);

        public Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixProblemDto>>([]);

        public Task<ZabbixEventContextDto?> GetEventContextAsync(
            string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<ZabbixEventContextDto?>(null);

        public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
            string itemId, DateTime fromUtc, DateTime toUtc, int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                History.TryGetValue(itemId, out var values)
                    ? values
                    : (IReadOnlyList<ZabbixHistoryValueDto>)[]);
    }
}
