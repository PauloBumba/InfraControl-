using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Devices.Queries;

// F1-b (Plano Mestre §8.1): estado operacional consolidado de um device.
// Combina sinais de (1) alertas abertos do monitoramento próprio, (2) host
// Zabbix vinculado via referência estável F29-1 (enabled/missing) e
// (3) problemas ativos do Zabbix para o host técnico correspondente.
// Regra: pior sinal vence (Unavailable > Degraded > Available); sem
// qualquer sinal → Unknown. A query usa BaseQuery + IAuthorizedRequest
// (RoleAccess.Read) com cache curto (30s) — o estado muda pouco e a
// chamada pode consultar o Zabbix ao vivo.

/// <summary>Estado consolidado de um device (§8.1).</summary>
public sealed record DeviceConsolidatedStatusDto(
    string Status,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Reasons,
    DateTime CheckedAtUtc);

public sealed record GetDeviceConsolidatedStatusQuery(Guid Id)
    : BaseQuery<DeviceConsolidatedStatusDto>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class DeviceConsolidatedStatusQueryHandler(
    IDeviceRepository deviceRepository,
    IAlertRepository alertRepository,
    IExternalDeviceReferenceRepository referenceRepository,
    IZabbixHostsRepository zabbixHostsRepository,
    IZabbixClient zabbixClient,
    IMonitoringResultRepository monitoringResultRepository,
    ILogger<DeviceConsolidatedStatusQueryHandler> logger)
    : IRequestHandler<GetDeviceConsolidatedStatusQuery, DeviceConsolidatedStatusDto>
{
    private const int OpenAlertsPageSize = 100;
    private const int RecentResultsCount = 10;

    public async Task<DeviceConsolidatedStatusDto> Handle(
        GetDeviceConsolidatedStatusQuery request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.Id}' não encontrado.");

        var sources = new List<string>();
        var reasons = new List<string>();
        var hasAvailable = false;
        var hasDegraded = false;
        var hasUnavailable = false;

        // 1. Alertas abertos do monitoramento próprio.
        var openAlerts = await alertRepository.SearchAsync(
            request.Id, AlertStatus.Open, 1, OpenAlertsPageSize, cancellationToken);
        if (openAlerts.TotalCount > 0)
        {
            sources.Add("Monitoring");
            foreach (var alertWithDevice in openAlerts.Items)
            {
                var alert = alertWithDevice.Alert;
                reasons.Add(
                    $"Alerta {alert.Severity} ({alert.Kind}): {alert.Message}");
                if (alert.Severity == AlertSeverity.Critical)
                    hasUnavailable = true;
                else
                    hasDegraded = true;
            }
        }

        // 2/3. Sinais do Zabbix via referência estável (F29-1).
        var references = await referenceRepository.ListByDeviceIdAsync(
            request.Id, cancellationToken);
        var zabbixReference = references.FirstOrDefault(reference =>
            reference.Source == ExternalDeviceSource.Zabbix);

        if (zabbixReference is not null && zabbixClient.IsConfigured)
        {
            var zabbixHost = await zabbixHostsRepository.GetByHostIdAsync(
                zabbixReference.ExternalId, cancellationToken);

            if (zabbixHost is null || zabbixHost.MissingSinceUtc is not null)
            {
                hasUnavailable = true;
                reasons.Add(
                    $"Host Zabbix '{zabbixReference.ExternalId}' ausente no Zabbix.");
            }
            else if (!zabbixHost.Enabled)
            {
                hasUnavailable = true;
                reasons.Add(
                    $"Host Zabbix '{zabbixHost.Host}' desabilitado.");
            }
            else
            {
                hasAvailable = true;
                reasons.Add(
                    $"Host Zabbix '{zabbixHost.Host}' habilitado (hostid {zabbixHost.HostId}).");
                sources.Add("Zabbix");

                try
                {
                    var problems = await zabbixClient.ListProblemsAsync(cancellationToken);
                    var matching = problems.Where(problem =>
                        problem.HostNames?.Contains(zabbixHost.Host,
                            StringComparer.OrdinalIgnoreCase) == true).ToList();

                    foreach (var problem in matching)
                    {
                        reasons.Add(
                            $"Problema Zabbix (sev {problem.Severity}): {problem.Name}");
                        if (problem.Severity >= 4)
                            hasUnavailable = true;
                        else
                            hasDegraded = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex,
                        "Falha ao consultar problemas do Zabbix para o status consolidado do device {DeviceId}",
                        request.Id);
                }
            }
        }

        // 4. Resultados recentes do monitoramento próprio (fallback de
        //    sinal quando não há alerta aberto nem Zabbix).
        if (openAlerts.TotalCount == 0
            && !references.Any(reference =>
                reference.Source == ExternalDeviceSource.Zabbix))
        {
            var recent = await monitoringResultRepository.GetRecentByDeviceAsync(
                request.Id, RecentResultsCount, cancellationToken);
            if (recent.Count > 0)
            {
                sources.Add("Monitoring");
                var failures = recent.Count(result => !result.Succeeded);
                if (failures > 0)
                {
                    hasDegraded = true;
                    reasons.Add(
                        $"{failures} de {recent.Count} verificações recentes falharam.");
                }
                else
                {
                    hasAvailable = true;
                    reasons.Add(
                        $"{recent.Count} verificações recentes bem-sucedidas.");
                }
            }
        }

        var status = hasUnavailable ? "Unavailable"
            : hasDegraded ? "Degraded"
            : hasAvailable ? "Available"
            : "Unknown";

        if (status == "Unknown")
            reasons.Add("Nenhum sinal de monitoramento vinculado a este device.");

        return new DeviceConsolidatedStatusDto(
            status,
            sources.Distinct(StringComparer.Ordinal).ToArray(),
            reasons,
            AppTimeProvider.Current.GetUtcNow().UtcDateTime);
    }
}
