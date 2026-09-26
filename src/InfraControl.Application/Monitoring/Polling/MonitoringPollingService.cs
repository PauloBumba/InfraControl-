using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Monitoring.Polling;

// F5-T007 (MON-010): polling periódico estilo Zabbix — a cada ciclo
// executa os checks habilitados (MonitoringCheck.Enabled) de todos os
// devices com IP. Padrão ICleanupService: regra aqui, worker fino só
// agenda. Um único SaveChanges por ciclo (mesma decisão do RunAll).

public sealed class MonitoringPollingService(
    IDeviceRepository deviceRepository,
    IMonitoringCheckRepository checkRepository,
    IProviderRegistry<IMonitoringProvider> providers,
    IMonitoringResultRepository resultRepository,
    ILogger<MonitoringPollingService> logger)
    : IMonitoringPollingService
{
    public async Task<int> PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);
        var enabled = await checkRepository.GetEnabledAsync(cancellationToken);
        var checksByDevice = enabled
            .GroupBy(check => check.DeviceId)
            .ToDictionary(group => group.Key, group => group.Select(check => check.Kind));

        var executed = 0;
        foreach (var device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.Ip))
                continue;
            if (!checksByDevice.TryGetValue(device.Id, out var kinds))
                continue;

            foreach (var kind in kinds)
            {
                try
                {
                    var result = await RunCheckCommandHandler.ExecuteAsync(
                        device, kind, providers, resultRepository, logger, cancellationToken);
                    if (result.Executed)
                        executed++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ArgumentException exception)
                {
                    // Guard de impressora (SnmpToner/Drum em non-Printer) ou
                    // provider não registrado — pulo com log, ciclo segue.
                    logger.LogWarning(
                        "Polling pulou {Check} em {DeviceId} ({Name}): {Reason}",
                        kind, device.Id, device.Name, exception.Message);
                }
                catch (Exception exception)
                {
                    // Erro de um check não aborta o ciclo nem perde os demais.
                    logger.LogError(
                        exception, "Polling: {Check} em {DeviceId} ({Name}) falhou.",
                        kind, device.Id, device.Name);
                }
            }
        }

        await resultRepository.SaveChangesAsync(cancellationToken);
        return executed;
    }
}
