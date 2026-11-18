using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Devices.Queries;

// F54-T001: overview agregado da frota para o dashboard — totais por tipo
// e status consolidado (Available/Degraded/Unavailable/Unknown) computados
// EM LOTE com a mesma regra do GetDeviceConsolidatedStatusQuery (pior
// sinal vence), sem N+1: uma query agrupada de alertas Open, uma chamada
// de problemas Zabbix e uma janela recente de monitoring_results.

/// <summary>Contagem por tipo de device (type = DeviceType.ToString()).</summary>
public sealed record DeviceTypeCountDto(string Type, int Count);

/// <summary>Contagem por status consolidado.</summary>
public sealed record DeviceStatusCountDto(string Status, int Count);

public sealed record DeviceFleetStatusDto(
    int TotalDevices,
    IReadOnlyList<DeviceTypeCountDto> ByType,
    IReadOnlyList<DeviceStatusCountDto> ByStatus,
    int OpenAlerts,
    int OpenCriticalAlerts,
    int OpenWarningAlerts);

/// <summary>Status operacional da frota inteira (dashboard F54).</summary>
public sealed record GetDeviceFleetStatusQuery
    : BaseQuery<DeviceFleetStatusDto>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class DeviceFleetStatusQueryHandler(
    IDeviceRepository deviceRepository,
    IAlertRepository alertRepository,
    IMonitoringResultRepository monitoringResultRepository,
    IExternalDeviceReferenceRepository referenceRepository,
    IZabbixHostsRepository zabbixHostsRepository,
    IZabbixClient zabbixClient,
    ILogger<DeviceFleetStatusQueryHandler> logger)
    : IRequestHandler<GetDeviceFleetStatusQuery, DeviceFleetStatusDto>
{
    /// <summary>Janela de polls recentes usada no fallback por device.</summary>
    private const int RecentResultsPerDevice = 10;

    /// <summary>
    /// Janela global de polls lida para o fallback — cobre ~10 polls por
    /// device numa frota média (polling de 60s); frota muito maior pode
    /// deixar devices sem sinal recente → Unknown (honesto, nunca inventa).
    /// </summary>
    private const int RecentResultsWindow = 5000;

    private static readonly string[] StatusOrder =
        ["Available", "Degraded", "Unavailable", "Unknown"];

    public async Task<DeviceFleetStatusDto> Handle(
        GetDeviceFleetStatusQuery request, CancellationToken cancellationToken)
    {
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);
        var deviceIds = devices.Select(d => d.Id).ToHashSet();

        // 1. Alertas Open agregados por (device, severidade) — uma query.
        var openCounts = await alertRepository.GetOpenCountsByDeviceAsync(cancellationToken);
        var alertsByDevice = openCounts
            .Where(c => deviceIds.Contains(c.DeviceId))
            .GroupBy(c => c.DeviceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 2. Referências Zabbix + hosts sincronizados + problemas ativos
        //    (uma chamada ao vivo, mesmo padrão do GetZabbixSummaryQuery).
        var zabbixReferences = (await referenceRepository.ListBySourceAsync(
                ExternalDeviceSource.Zabbix, cancellationToken))
            .Where(r => deviceIds.Contains(r.DeviceId))
            .GroupBy(r => r.DeviceId)
            .ToDictionary(g => g.Key, g => g.First());

        var zabbixHosts = (await zabbixHostsRepository.ListAsync(cancellationToken))
            .ToDictionary(h => h.HostId, StringComparer.Ordinal);

        // Severidade máxima de problema NÃO recuperado por nome de host.
        var problemSeverityByHost = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (zabbixClient.IsConfigured)
        {
            try
            {
                var problems = await zabbixClient.ListProblemsAsync(cancellationToken);
                foreach (var problem in problems)
                {
                    // Só problemas ativos degradam a frota — o mesmo
                    // critério do GetZabbixProblemsQuery (IncludeRecovered=false).
                    if (problem.RecoveryEventId is not null)
                        continue;
                    foreach (var hostName in problem.HostNames ?? [])
                    {
                        if (!problemSeverityByHost.TryGetValue(hostName, out var current)
                            || problem.Severity > current)
                            problemSeverityByHost[hostName] = problem.Severity;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Falha ao consultar problemas do Zabbix para o status da frota");
                problemSeverityByHost.Clear();
            }
        }

        // 3. Janela recente de polls para o fallback de devices sem
        //    alertas e sem referência Zabbix (mesma regra do status
        //    consolidado: últimos N resultados do device).
        var recentResults = await monitoringResultRepository.GetRecentAsync(
            RecentResultsWindow, cancellationToken);
        var recentByDevice = recentResults
            .GroupBy(r => r.DeviceId)
            .ToDictionary(g => g.Key, g => g.Take(RecentResultsPerDevice).ToList());

        var byStatus = StatusOrder.ToDictionary(s => s, _ => 0);
        foreach (var device in devices)
        {
            var status = ResolveStatus(
                device.Id,
                alertsByDevice,
                zabbixReferences,
                zabbixHosts,
                problemSeverityByHost,
                zabbixClient.IsConfigured,
                recentByDevice);
            byStatus[status]++;
        }

        return new DeviceFleetStatusDto(
            devices.Count,
            devices
                .GroupBy(d => d.Type.ToString())
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new DeviceTypeCountDto(g.Key, g.Count()))
                .ToList(),
            StatusOrder
                .Select(s => new DeviceStatusCountDto(s, byStatus[s]))
                .ToList(),
            openCounts.Sum(c => c.Count),
            openCounts.Where(c => c.Severity == AlertSeverity.Critical).Sum(c => c.Count),
            openCounts.Where(c => c.Severity == AlertSeverity.Warning).Sum(c => c.Count));
    }

    /// <summary>
    /// Mesma regra do DeviceConsolidatedStatusQueryHandler: alertas Open
    /// (Critical→Unavailable, Warning→Degraded); Zabbix vinculado e
    /// configurado → host ausente/desabilitado = Unavailable, habilitado =
    /// Available e problemas ativos sev≥4 = Unavailable / &lt;4 = Degraded;
    /// sem nenhum sinal → resultados recentes (falhas = Degraded, tudo ok =
    /// Available); sem nada → Unknown.
    /// </summary>
    private static string ResolveStatus(
        Guid deviceId,
        IReadOnlyDictionary<Guid, List<OpenAlertCount>> alertsByDevice,
        IReadOnlyDictionary<Guid, ExternalDeviceReference> zabbixReferences,
        IReadOnlyDictionary<string, ZabbixHost> zabbixHosts,
        IReadOnlyDictionary<string, int> problemSeverityByHost,
        bool zabbixConfigured,
        IReadOnlyDictionary<Guid, List<MonitoringResult>> recentByDevice)
    {
        var hasUnavailable = false;
        var hasDegraded = false;
        var hasAvailable = false;

        var openAlerts = alertsByDevice.TryGetValue(deviceId, out var alerts)
            ? alerts
            : [];
        if (openAlerts.Any(a => a.Severity == AlertSeverity.Critical))
            hasUnavailable = true;
        else if (openAlerts.Count > 0)
            hasDegraded = true;

        var hasZabbixReference = zabbixReferences.ContainsKey(deviceId);
        if (hasZabbixReference && zabbixConfigured)
        {
            var zabbixHost = zabbixHosts.TryGetValue(
                zabbixReferences[deviceId].ExternalId, out var host) ? host : null;

            if (zabbixHost is null || zabbixHost.MissingSinceUtc is not null || !zabbixHost.Enabled)
            {
                hasUnavailable = true;
            }
            else
            {
                hasAvailable = true;
                if (problemSeverityByHost.TryGetValue(zabbixHost.Host, out var severity))
                {
                    if (severity >= 4)
                        hasUnavailable = true;
                    else
                        hasDegraded = true;
                }
            }
        }

        // Fallback idêntico ao consolidado: só quando não há alertas Open
        // nem referência Zabbix (de qualquer tipo de configuração).
        if (openAlerts.Count == 0 && !hasZabbixReference
            && recentByDevice.TryGetValue(deviceId, out var recent) && recent.Count > 0)
        {
            if (recent.Any(r => !r.Succeeded))
                hasDegraded = true;
            else
                hasAvailable = true;
        }

        return hasUnavailable ? "Unavailable"
            : hasDegraded ? "Degraded"
            : hasAvailable ? "Available"
            : "Unknown";
    }
}
