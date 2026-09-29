using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.SignalR;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal SignalR (F9-T004, docs/07): consome as
/// deliveries `Pending`, envia via `ISignalRNotificationDispatcher` e
/// marca `Sent`/`Failed`. Retry/backoff/DLQ em F9-T009.
/// </summary>
public sealed class NotificationSignalRWorker(
    IServiceScopeFactory scopeFactory,
    ISignalRNotificationDispatcher dispatcher,
    ILogger<NotificationSignalRWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "NotificationSignalRWorker iniciado (intervalo {Interval}).", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception, "Falha ao processar deliveries SignalR — nova tentativa no próximo ciclo.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Um lote de pendentes — exposto para teste (F9-T004).</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();

        var pending = await repository.ListPendingByChannelAsync(
            NotificationChannel.SignalR, limit: 50, cancellationToken);

        var sent = 0;
        foreach (var item in pending)
        {
            // Trackável (sem AsNoTracking no GetById) — Update espera linha.
            var delivery = await repository.GetByIdAsync(item.DeliveryId, cancellationToken);
            if (delivery is null)
                continue;

            try
            {
                await dispatcher.SendAsync(new NotificationSignalRMessage(
                    item.DeliveryId, item.NotificationId, item.UserId,
                    item.Title, item.Message, item.CreatedAt),
                    cancellationToken);

                delivery.MarkSent(AppTimeProvider.Current.GetUtcNow().UtcDateTime);
                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                delivery.MarkFailed(exception.Message);
                logger.LogWarning(
                    exception,
                    "Delivery {DeliveryId} falhou no canal SignalR (usuário {UserId}).",
                    item.DeliveryId, item.UserId);
            }
        }

        if (pending.Count > 0)
            await repository.SaveChangesAsync(cancellationToken);

        if (sent > 0)
            logger.LogInformation("{Count} deliveries SignalR enviadas.", sent);

        return sent;
    }
}
