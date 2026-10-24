using FluentAssertions;
using InfraControl.Application.Alerts;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Prevention;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T004 S4 (ADR-056 aceito): EvaluateWifiPreventionCommand — device →
/// host Zabbix (por IP) → itens por chave configurada → histórico da
/// janela → avaliador puro (S4a) → Alert pelo MESMO ciclo de vida dos
/// checks (IAlertEvaluator).
///
/// Opt-in = o device tem SnmpRadio no perfil (configuração explícita por
/// device que já existe — sem migração). REGRA da casa: pré-requisito
/// ausente (host/item/chave/histórico) = avaliação honesta com motivo em
/// pt-BR e Alerta INTOCADO — nunca "saudável" inventado.
/// </summary>
public sealed class WifiPreventionCommandTests
{
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly DateTime T0 = DateTime.UtcNow.AddMinutes(-55);

    private static Device AccessPoint(string ip = "192.0.2.30", bool optedIn = true)
    {
        var device = Device.Create(
            LocationId, "ap-sala-01", DeviceType.AccessPoint, ip: ip);
        if (optedIn)
            device.ApplyCustomProfile([CheckKind.Ping, CheckKind.SnmpRadio]);
        return device;
    }

    private static ZabbixHost Host(string ip) => ZabbixHost.Create(
        "10101", "ap-sala-01", "AP Sala 01", ip, null,
        enabled: true, discovered: true, [], [], DateTime.UtcNow);

    private static IReadOnlyList<ZabbixHistoryValueDto> Series(
        string itemId, params double[] values) =>
        values.Select((value, index) =>
            new ZabbixHistoryValueDto(
                itemId, T0.AddMinutes(index * 5),
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

    private static WifiPreventionOptions Options(
        string utilization = "wifi.utilization",
        string rssi = "wifi.rssi",
        string clients = "wifi.clients")
        => new()
        {
            UtilizationItemKey = utilization,
            RssiItemKey = rssi,
            ClientsItemKey = clients,
        };

    private static EvaluateWifiPreventionCommandHandler Handler(
        Device device,
        FakeZabbixHostsRepository? hosts = null,
        FakeZabbixClient? zabbix = null,
        FakeAlertEvaluator? alerts = null,
        WifiPreventionOptions? options = null)
        => new(
            new FakeDeviceRepository(device),
            hosts ?? new FakeZabbixHostsRepository(Host(device.Ip!)),
            zabbix ?? new FakeZabbixClient(),
            alerts ?? new FakeAlertEvaluator(),
            Microsoft.Extensions.Options.Options.Create(options ?? Options()),
            LoggerFactory.Create(builder => { }).CreateLogger<EvaluateWifiPreventionCommandHandler>());

    [Fact]
    public async Task UnknownDevice_ShouldThrowKeyNotFound()
    {
        var handler = Handler(AccessPoint());

        var action = () => handler.Handle(
            new EvaluateWifiPreventionCommand(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task NonAccessPoint_ShouldThrowArgumentPtBrMessage()
    {
        var switchDevice = Device.Create(
            LocationId, "sw-01", DeviceType.Switch, ip: "192.0.2.10");
        var handler = Handler(switchDevice);

        var action = () => handler.Handle(
            new EvaluateWifiPreventionCommand(switchDevice.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*só se aplica a devices do tipo AccessPoint*");
    }

    [Fact]
    public async Task NotOptedIn_ShouldReturnHonestReason_WithoutEvaluating()
    {
        var device = AccessPoint(optedIn: false);
        var alerts = new FakeAlertEvaluator();
        var handler = Handler(device, alerts: alerts);

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeFalse();
        result.Reason.Should().Contain("SnmpRadio");
        alerts.VerdictCalls.Should().Be(0);
        alerts.ResolveCalls.Should().Be(0);
    }

    [Fact]
    public async Task WithoutZabbixHostForIp_ShouldReturnHonestReason()
    {
        var device = AccessPoint();
        var handler = Handler(device, hosts: new FakeZabbixHostsRepository(host: null));

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeFalse();
        result.Reason.Should().Contain("host Zabbix");
    }

    [Fact]
    public async Task WhenZabbixIsNotConfigured_ShouldReturnHonestReason()
    {
        var device = AccessPoint();
        var handler = Handler(
            device, zabbix: new FakeZabbixClient { IsConfigured = false });

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeFalse();
        result.Reason.Should().Contain("Integração Zabbix");
    }

    [Fact]
    public async Task WithoutConfiguredItemKeys_ShouldSkipAllRules_Honestly()
    {
        var device = AccessPoint();
        var alerts = new FakeAlertEvaluator();
        var handler = Handler(
            device, alerts: alerts,
            options: Options(utilization: "", rssi: "", clients: ""));

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeFalse();
        result.Rules.Should().HaveCount(5);
        result.Rules.Should().OnlyContain(rule => rule.Status == "Skipped");
        result.Rules.Should().OnlyContain(
            rule => rule.Reason!.Contains("chave de item não configurada"));
        alerts.VerdictCalls.Should().Be(0);
        alerts.ResolveCalls.Should().Be(0);
    }

    [Fact]
    public async Task SustainedSaturation_ShouldOpenCriticalAlert_ViaEvaluator()
    {
        var device = AccessPoint();
        var zabbix = new FakeZabbixClient();
        zabbix.Items.Add(new ZabbixItemDto(
            "501", "10101", "Utilização", "wifi.utilization",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.Items.Add(new ZabbixItemDto(
            "502", "10101", "RSSI", "wifi.rssi",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.Items.Add(new ZabbixItemDto(
            "503", "10101", "Clientes", "wifi.clients",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.History["501"] = Series(
            "501", 30, 32, 35, 38, 40, 45, 50, 55, 60, 85, 92, 95);
        zabbix.History["502"] = Series(
            "502", -55, -55, -55, -55, -55, -55, -55, -55, -55, -55, -55, -55);
        zabbix.History["503"] = Series(
            "503", 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        var alerts = new FakeAlertEvaluator();
        var handler = Handler(device, zabbix: zabbix, alerts: alerts);

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeTrue();
        result.AlertsOpened.Should().Be(1);
        var verdict = alerts.LastVerdict;
        verdict.Should().NotBeNull();
        verdict!.Value.Kind.Should().Be(CheckKind.SnmpRadio);
        verdict.Value.Severity.Should().Be(AlertSeverity.Critical);
        verdict.Value.Message.Should().Contain("95");
        result.Rules.Single(rule => rule.Rule == "radio-saturation-critical")
            .Status.Should().Be("Fired");
    }

    [Fact]
    public async Task HealthyHistory_ShouldResolveActiveAlert()
    {
        var device = AccessPoint();
        var zabbix = new FakeZabbixClient();
        zabbix.Items.Add(new ZabbixItemDto(
            "501", "10101", "Utilização", "wifi.utilization",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.History["501"] = Series(
            "501", 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30);
        var alerts = new FakeAlertEvaluator { ResolveResult = true };
        var handler = Handler(device, zabbix: zabbix, alerts: alerts);

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeTrue();
        result.AlertsResolved.Should().Be(1);
        alerts.ResolveCalls.Should().Be(1);
        alerts.VerdictCalls.Should().Be(0);
    }

    [Fact]
    public async Task MetricWithMissingItem_ShouldSkipOnlyThatMetric()
    {
        var device = AccessPoint();
        var zabbix = new FakeZabbixClient();
        zabbix.Items.Add(new ZabbixItemDto(
            "501", "10101", "Utilização", "wifi.utilization",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.History["501"] = Series(
            "501", 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30);
        var handler = Handler(device, zabbix: zabbix);

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeTrue();
        result.Rules.Single(rule => rule.Rule == "radio-saturation")
            .Status.Should().Be("Clean");
        var density = result.Rules.Single(rule => rule.Rule == "client-density");
        density.Status.Should().Be("Skipped");
        density.Reason.Should().Contain("ausente");
    }

    [Fact]
    public async Task HistoryWithUnparsableValues_ShouldCountHonestSampleShortage()
    {
        var device = AccessPoint();
        var zabbix = new FakeZabbixClient();
        zabbix.Items.Add(new ZabbixItemDto(
            "501", "10101", "Utilização", "wifi.utilization",
            Type: 0, Enabled: true, Unsupported: false));
        zabbix.History["501"] = [
            new ZabbixHistoryValueDto("501", T0, "abc"),
            new ZabbixHistoryValueDto("501", T0.AddMinutes(5), "30"),
            new ZabbixHistoryValueDto("501", T0.AddMinutes(10), "NaN-x"),
        ];
        var handler = Handler(device, zabbix: zabbix);

        var result = await handler.Handle(
            new EvaluateWifiPreventionCommand(device.Id), CancellationToken.None);

        result.Evaluated.Should().BeTrue();
        var saturation = result.Rules.Single(rule => rule.Rule == "radio-saturation");
        saturation.Status.Should().Be("Skipped");
        saturation.Reason.Should().Contain("1 de 12");
    }

    // -------------------------------------------------------------------
    // Fakes
    // -------------------------------------------------------------------

    private sealed class FakeDeviceRepository(Device device) : IDeviceRepository
    {
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(device.Id == id ? device : null);

        public Task AddAsync(Device entity, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Remove(Device entity) => throw new NotImplementedException();

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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeZabbixHostsRepository(ZabbixHost? host) : IZabbixHostsRepository
    {
        public Task<ZabbixHost?> GetByHostIdAsync(
            string hostId, CancellationToken cancellationToken = default)
            => Task.FromResult(host);

        public Task<IReadOnlyList<ZabbixHost>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixHost>>(host is null ? [] : [host]);

        public Task<PagedResult<ZabbixHost>> SearchAsync(
            string? term, bool? enabled, bool? missing, int page, int pageSize,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ZabbixHostCounts> GetCountsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AddAsync(ZabbixHost entity, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeZabbixClient : IZabbixClient
    {
        public bool IsConfigured { get; set; } = true;
        public List<ZabbixItemDto> Items { get; } = [];
        public Dictionary<string, IReadOnlyList<ZabbixHistoryValueDto>> History { get; } = [];

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("8.0.0");

        public Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixHostDto>>([]);

        public Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixHostGroupDto>>([]);

        public Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixTemplateDto>>([]);

        public Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
            string hostId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixItemDto>>(Items);

        public Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixTriggerDto>>([]);

        public Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZabbixProblemDto>>([]);

        public Task<ZabbixEventContextDto?> GetEventContextAsync(
            string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<ZabbixEventContextDto?>(null);

        public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
            string itemId, DateTime fromUtc, DateTime toUtc, int limit,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ZabbixHistoryValueDto> empty = [];
            return Task.FromResult(
                History.TryGetValue(itemId, out var values) ? values : empty);
        }
    }

    private sealed class FakeAlertEvaluator : IAlertEvaluator
    {
        public int VerdictCalls { get; private set; }
        public int ResolveCalls { get; private set; }
        public bool VerdictResult { get; set; } = true;
        public bool ResolveResult { get; set; }

        public (Device Device, CheckKind Kind, AlertSeverity Severity, string Message)? LastVerdict
        { get; private set; }

        public Task EvaluateAsync(
            Device device, CheckKind kind, MonitoringResult result,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ExternalEvaluationResult> EvaluateExternalAsync(
            MonitoringEvent monitoringEvent, string? host, string? ip,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> EvaluateVerdictAsync(
            Device device, CheckKind kind, AlertSeverity severity, string message,
            CancellationToken cancellationToken = default)
        {
            VerdictCalls++;
            LastVerdict = (device, kind, severity, message);
            return Task.FromResult(VerdictResult);
        }

        public Task<bool> ResolveVerdictAsync(
            Device device, CheckKind kind, CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return Task.FromResult(ResolveResult);
        }
    }
}
