using InfraControl.Application.Alerts.Ports;
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
    IMonitoringResultRepository resultRepository) : IAlertEvaluator
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
                    await ResolveOpenAsync(device.Id, kind, cancellationToken);
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
                        await OpenAsync(
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
            await OpenAsync(
                device, kind, AlertSeverity.Warning,
                $"{label} de '{device.Name}' em {percent}% (mínimo {minPercent}%).",
                cancellationToken);
        }
        else
        {
            await ResolveOpenAsync(device.Id, kind, cancellationToken);
        }
    }

    private async Task OpenAsync(
        Device device,
        CheckKind kind,
        AlertSeverity severity,
        string message,
        CancellationToken cancellationToken)
    {
        // Anti alert-storm: 1 alerta Open por (device, check).
        var open = await repository.GetOpenAsync(device.Id, kind, cancellationToken);
        if (open is not null)
            return;

        await repository.AddAsync(
            Alert.Create(device.Id, kind, severity, message), cancellationToken);
    }

    private async Task ResolveOpenAsync(
        Guid deviceId, CheckKind kind, CancellationToken cancellationToken)
    {
        var open = await repository.GetOpenAsync(deviceId, kind, cancellationToken);
        open?.Resolve();
    }
}
