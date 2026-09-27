using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

// F6-T001/F6-T002: thresholds e regras FIXOS em código (decisão do
// usuário — generalizar para perfil/DeviceProfile fica para task+ADR
// futura). Avaliação pós-check; anti-storm = 1 Open por (device, check) +
// streak de falhas consecutivas no histórico (N=3 para Ping).

public static class AlertThresholds
{
    public const int TonerMinPercent = 20;
    public const int DrumMinPercent = 10;

    /// <summary>Falhas consecutivas de Ping antes de abrir o alerta (F6-T002).</summary>
    public const int PingFailureStreak = 3;
}

/// <summary>
/// Avalia o resultado de um check e cria/resolve o Alert correspondente
/// (pós-check, mesmo SaveChanges do MonitoringResult).
/// </summary>
public interface IAlertEvaluator
{
    Task EvaluateAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken = default);
}

public sealed class AlertEvaluator(
    IAlertRepository repository,
    IMonitoringResultRepository resultRepository,
    IIncidentRepository incidentRepository,
    IUpstreamCorrelationRepository correlationRepository,
    IRootCauseResolver rootCauseResolver) : IAlertEvaluator
{
    public async Task EvaluateAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken = default)
    {
        switch (kind)
        {
            case CheckKind.Ping:
                if (result.Succeeded)
                {
                    await ResolveActiveAsync(device.Id, kind, cancellationToken);
                }
                else
                {
                    // F6-T002: só abre com N falhas consecutivas — N-1 do
                    // histórico (banco) + o atual. Sucesso salvo no meio
                    // (próximo ciclo) quebra a sequência.
                    var required = AlertThresholds.PingFailureStreak - 1;
                    var previous = await resultRepository
                        .GetRecentByDeviceAndKindAsync(
                            device.Id, kind, required, cancellationToken);

                    if (previous.Count >= required
                        && previous.All(entry => !entry.Succeeded))
                    {
                        await OpenOrSuppressAsync(
                            device, kind, AlertSeverity.Critical,
                            $"Device '{device.Name}' fora do ar "
                                + $"({AlertThresholds.PingFailureStreak} falhas "
                                + "consecutivas no ping).",
                            cancellationToken);
                    }
                }
                break;

            case CheckKind.SnmpToner:
                await EvaluateSupplyAsync(
                    device, kind, result, AlertThresholds.TonerMinPercent, "Toner",
                    cancellationToken);
                break;

            case CheckKind.SnmpDrum:
                await EvaluateSupplyAsync(
                    device, kind, result, AlertThresholds.DrumMinPercent, "Drum",
                    cancellationToken);
                break;

            default:
                // F6-T001 cobre só Ping/toner/drum; demais kinds são F6-T002+.
                break;
        }

        // F6-T005: reavalia a causa provável do Incident Open do device
        // (cobre a corrida: upstream/vizinho abriram depois; incidente
        // recém-criado ganhou causa direto no EnsureIncidentAsync).
        await rootCauseResolver.RefreshAsync(device.Id, cancellationToken);
    }

    private async Task EvaluateSupplyAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        int minPercent,
        string label,
        CancellationToken cancellationToken)
    {
        // Erro de SNMP (Succeeded=false, Values=null) ou parse falho →
        // nível desconhecido: não avalia (não é "baixo").
        if (result.Values is null
            || !result.Values.TryGetValue("levelPercent", out var raw)
            || !int.TryParse(raw, out var percent))
            return;

        if (percent < minPercent)
        {
            // Warning não toca incidente (só Critical auto-abre — F6-T003).
            await OpenOrSuppressAsync(
                device, kind, AlertSeverity.Warning,
                $"{label} de '{device.Name}' em {percent}% (mínimo {minPercent}%).",
                cancellationToken);
        }
        else
        {
            await ResolveActiveAsync(device.Id, kind, cancellationToken);
        }
    }

    /// <summary>
    /// F6-T004: com upstream alcançável (BFS nas Connections) em Incident
    /// Open, o alerta de qualquer kind nasce/vira <see cref="AlertStatus.Suppressed"/>
    /// e é vinculado ao incidente raiz (N:N) — sem incidente próprio. Sem
    /// upstream: fluxo normal (Open; Critical auto-abre Incident — F6-T003;
    /// alerta Suppressed libera para Open).
    /// </summary>
    private async Task OpenOrSuppressAsync(
        Device device,
        CheckKind kind,
        AlertSeverity severity,
        string message,
        CancellationToken cancellationToken)
    {
        var upstream = await correlationRepository
            .FindOpenIncidentUpstreamAsync(device.Id, cancellationToken);
        var active = await repository
            .GetActiveAsync(device.Id, kind, cancellationToken);

        if (upstream is not null)
        {
            var alert = active;
            if (alert is null)
            {
                alert = Alert.Create(device.Id, kind, severity, message);
                alert.Suppress();
                await repository.AddAsync(alert, cancellationToken);
            }
            else
            {
                alert.Suppress();
            }

            // Idempotente no repositório (já vinculado → skip).
            await incidentRepository.AddLinkAsync(
                IncidentAlert.Create(upstream.IncidentId, alert.Id),
                cancellationToken);
            return;
        }

        if (active is null)
        {
            active = Alert.Create(device.Id, kind, severity, message);
            await repository.AddAsync(active, cancellationToken);
        }
        else
        {
            active.Reopen();
        }

        if (severity == AlertSeverity.Critical)
        {
            // F6-T003: Alert Critical auto-abre Incident.
            await EnsureIncidentAsync(device, active, cancellationToken);
        }
    }

    private async Task ResolveActiveAsync(
        Guid deviceId, CheckKind kind, CancellationToken cancellationToken)
    {
        // Resolve Open e Suppressed (F6-T004) — condição normalizada.
        var active = await repository.GetActiveAsync(deviceId, kind, cancellationToken);
        active?.Resolve();
    }

    /// <summary>
    /// F6-T003: Alert Critical garante 1 Incident Open por device e
    /// vincula o alerta (N:N); alerta órfão (criado antes do T003) ganha
    /// vínculo na próxima avaliação.
    /// </summary>
    private async Task EnsureIncidentAsync(
        Device device, Alert alert, CancellationToken cancellationToken)
    {
        var incident = await incidentRepository.GetOpenByDeviceAsync(
            device.Id, cancellationToken);

        if (incident is null)
        {
            incident = Incident.Create(
                alert.Message, IncidentSeverity.Critical, deviceId: device.Id);
            incident.SetRootCause(
                await rootCauseResolver.ResolveAsync(incident, cancellationToken));
            await incidentRepository.AddAsync(incident, cancellationToken);
        }

        await incidentRepository.AddLinkAsync(
            IncidentAlert.Create(incident.Id, alert.Id), cancellationToken);
    }
}
