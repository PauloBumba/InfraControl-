using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// F6-T005: causa provável por topologia + histórico de alertas —
/// regras fixas (docs/09): upstream com incidente → causa nele; vizinho
/// caído ANTES do incidente → enlace/switch do vizinho; senão → falha
/// local. A regra de tempo evita inverter a culpa (vítimas caem depois).
/// </summary>
public sealed class RootCauseResolver(
    IConnectionRepository connectionRepository,
    IAlertRepository alertRepository,
    IDeviceRepository deviceRepository,
    IUpstreamCorrelationRepository correlationRepository,
    IIncidentRepository incidentRepository) : IRootCauseResolver
{
    public async Task<string?> ResolveAsync(
        Incident incident, CancellationToken cancellationToken = default)
    {
        if (incident.DeviceId is not { } deviceId)
            return null; // incidente manual sem device não tem causa derivada

        var upstream = await correlationRepository
            .FindOpenIncidentUpstreamAsync(deviceId, cancellationToken);
        if (upstream is not null)
        {
            var root = await deviceRepository.GetByIdAsync(upstream.DeviceId, cancellationToken);
            return root is null
                ? "Falha em um upstream conectado (incidente aberto)."
                : $"Falha no upstream '{root.Name}' — device atrás de um incidente aberto.";
        }

        var neighbors = await connectionRepository
            .GetNeighborDeviceIdsAsync(deviceId, cancellationToken);
        foreach (var neighborId in neighbors)
        {
            var alert = await alertRepository
                .GetActiveAsync(neighborId, CheckKind.Ping, cancellationToken);

            // Só Open (Suppressed = vítima do mesmo root) e mais antigo
            // que o incidente (caiu primeiro → causa plausível).
            if (alert is null
                || alert.Status != AlertStatus.Open
                || alert.CreateDateTime >= incident.CreateDateTime)
                continue;

            var neighbor = await deviceRepository.GetByIdAsync(neighborId, cancellationToken);
            return neighbor is null
                ? "Vizinho sem resposta — provável enlace ou switch intermediário."
                : $"Vizinho '{neighbor.Name}' sem resposta — provável enlace ou switch intermediário.";
        }

        return "Falha local no device (energia, processo ou enlace direto).";
    }

    public async Task RefreshAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var incident = await incidentRepository
            .GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (incident is null)
            return;

        incident.SetRootCause(await ResolveAsync(incident, cancellationToken));
    }
}
