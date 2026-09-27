using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

// F6-T001: thresholds FIXOS em código (decisão do usuário — generalizar
// para perfil/DeviceProfile fica para F6-T002+ADR). Avaliação pós-check.

public static class AlertThresholds
{
    public const int TonerMinPercent = 20;
    public const int DrumMinPercent = 10;
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

public sealed class AlertEvaluator(IAlertRepository repository) : IAlertEvaluator
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
                    await ResolveOpenAsync(device.Id, kind, cancellationToken);
                else
                    await OpenAsync(
                        device, kind, AlertSeverity.Critical,
                        $"Device '{device.Name}' está fora do ar (ping falhou).",
                        cancellationToken);
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
