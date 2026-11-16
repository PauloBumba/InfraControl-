using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Dashboard.Queries;

// F54-T002 (docs/69 §2.2): visão geral do dashboard — UMA chamada com as
// contagens globais dos KPIs do topo. Agregado em cargas únicas (sem N+1
// por device) e somente com sinais LOCAIS do próprio InfraControl
// (inventário, alertas abertos, incidentes, último ping): o status
// consolidado detalhado com Zabbix continua em GetDeviceConsolidatedStatusQuery
// (cache 30s) e os dados ao vivo do UniFi/Zabbix vêm dos endpoints
// próprios de cada aba do dashboard. Zero dado fictício (D5).

/// <summary>Contagens de dispositivos do dashboard (F54-T002).</summary>
public sealed record DashboardDevicesDto(
    int Total,
    IReadOnlyDictionary<string, int> ByType,
    int Online,
    int Degraded,
    int Offline,
    int Unknown,
    int WithAlerts);

/// <summary>Alertas abertos do monitoramento próprio (F54-T002).</summary>
public sealed record DashboardAlertsDto(
    int Open,
    int Critical,
    int Warning);

/// <summary>Incidentes não resolvidos (F54-T002).</summary>
public sealed record DashboardIncidentsDto(int Active);

/// <summary>Impressoras e suprimento (toner/cilindro) em alerta (F54-T002).</summary>
public sealed record DashboardPrintersDto(
    int Total,
    int WithSupplyAlert);

public sealed record DashboardOverviewDto(
    DateTime GeneratedAtUtc,
    DashboardDevicesDto Devices,
    DashboardAlertsDto Alerts,
    DashboardIncidentsDto Incidents,
    DashboardPrintersDto Printers);

public sealed record GetDashboardOverviewQuery
    : BaseQuery<DashboardOverviewDto>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetDashboardOverviewQueryHandler(
    IDeviceRepository deviceRepository,
    IAlertRepository alertRepository,
    IIncidentRepository incidentRepository,
    IMonitoringResultRepository monitoringResultRepository)
    : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewDto>
{
    // Clamp do SearchAsync é 1..100 por página; o teto de 10 páginas
    // (1000 alertas abertos) protege o agregado de um loop desenfreado —
    // acima disso as contagens ficam por baixo (cenário não observado no
    // monitoramento próprio, que é fonte local destes alertas).
    private const int OpenAlertsPageSize = 100;
    private const int MaxOpenAlertPages = 10;

    public async Task<DashboardOverviewDto> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var devices = await deviceRepository
            .GetAllOrderedByNameAsync(cancellationToken);
        var openAlerts = await ListOpenAlertsAsync(cancellationToken);

        // Incidentes ativos = todos os estados exceto Resolved (F6-T003);
        // só o TotalCount importa — página mínima 1.
        var incidents = await incidentRepository.SearchAsync(
            null,
            [
                IncidentStatus.Open,
                IncidentStatus.Investigating,
                IncidentStatus.Identified,
                IncidentStatus.Monitoring
            ],
            1, 1, cancellationToken);

        var latestPings = (await monitoringResultRepository
                .GetLatestPingByDeviceAsync(cancellationToken))
            .ToDictionary(result => result.DeviceId);

        var alertsByDevice = openAlerts
            .Where(alert => alert.DeviceId.HasValue)
            .GroupBy(alert => alert.DeviceId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var online = 0;
        var degraded = 0;
        var offline = 0;
        var unknown = 0;
        var withAlerts = 0;

        foreach (var device in devices)
        {
            alertsByDevice.TryGetValue(device.Id, out var deviceAlerts);
            if (deviceAlerts is { Count: > 0 })
                withAlerts++;

            var hasCritical = deviceAlerts?
                .Any(alert => alert.Severity == AlertSeverity.Critical) == true;
            var hasWarning = deviceAlerts?
                .Any(alert => alert.Severity == AlertSeverity.Warning) == true;
            latestPings.TryGetValue(device.Id, out var lastPing);

            // Mesma precedência do status consolidado (pior sinal vence):
            // Offline (alerta crítico = down, ou último ping falhou) >
            // Degraded (alerta de aviso) > Online (ping ok) > Unknown.
            if (hasCritical || lastPing is { Succeeded: false })
                offline++;
            else if (hasWarning)
                degraded++;
            else if (lastPing is { Succeeded: true })
                online++;
            else
                unknown++;
        }

        var byType = devices
            .GroupBy(device => device.Type)
            .ToDictionary(group => group.Key.ToString(), group => group.Count());

        // Suprimento em alerta: dispositivo IMPRESSORA distinto com alerta
        // aberto de toner/cilindro (mono ou colorido).
        var printersWithSupplyAlert = openAlerts
            .Where(alert => alert.DeviceId.HasValue
                && alert.Kind is CheckKind.SnmpToner or CheckKind.SnmpDrum
                    or CheckKind.SnmpColorToner)
            .Select(alert => alert.DeviceId!.Value)
            .Distinct()
            .Count();

        return new DashboardOverviewDto(
            AppTimeProvider.Current.GetUtcNow().UtcDateTime,
            new DashboardDevicesDto(
                devices.Count,
                byType,
                online,
                degraded,
                offline,
                unknown,
                withAlerts),
            new DashboardAlertsDto(
                openAlerts.Count,
                openAlerts.Count(alert =>
                    alert.Severity == AlertSeverity.Critical),
                openAlerts.Count(alert =>
                    alert.Severity == AlertSeverity.Warning)),
            new DashboardIncidentsDto(incidents.TotalCount),
            new DashboardPrintersDto(
                byType.GetValueOrDefault(nameof(DeviceType.Printer)),
                printersWithSupplyAlert));
    }

    private async Task<List<Alert>> ListOpenAlertsAsync(
        CancellationToken cancellationToken)
    {
        var alerts = new List<Alert>();
        for (var page = 1; page <= MaxOpenAlertPages; page++)
        {
            var current = await alertRepository.SearchAsync(
                null, AlertStatus.Open, page, OpenAlertsPageSize,
                cancellationToken);
            alerts.AddRange(current.Items.Select(item => item.Alert));
            if (current.Items.Count == 0 || alerts.Count >= current.TotalCount)
                break;
        }

        return alerts;
    }
}
