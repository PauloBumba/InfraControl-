using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.Workers;

/// <summary>
/// Loop comum dos workers de canal (F9-T004 SignalR, F9-T005 Email):
/// consome as deliveries `Pending` do canal, despacha e marca
/// `Sent`/`Failed`. Retry/backoff/DLQ em F9-T009.
/// </summary>
public abstract class NotificationChannelWorkerBase(
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    NotificationChannel channel)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected abstract Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker de notificações do canal {Channel} iniciado (intervalo {Interval}).",
            channel, Interval);

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
                    exception,
                    "Falha ao processar deliveries do canal {Channel} — nova tentativa no próximo ciclo.",
                    channel);
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

    /// <summary>Um lote de pendentes — exposto para teste.</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();

        var pending = await repository.ListPendingByChannelAsync(
            channel, limit: 50, cancellationToken);

        var sent = 0;
        foreach (var item in pending)
        {
            // Trackável (sem AsNoTracking no GetById) — Update espera linha.
            var delivery = await repository.GetByIdAsync(item.DeliveryId, cancellationToken);
            if (delivery is null)
                continue;

            try
            {
                await DispatchAsync(item, delivery, cancellationToken);
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
                    "Delivery {DeliveryId} falhou no canal {Channel} (usuário {UserId}).",
                    item.DeliveryId, channel, item.UserId);
            }
        }

        if (pending.Count > 0)
            await repository.SaveChangesAsync(cancellationToken);

        if (sent > 0)
            logger.LogInformation(
                "{Count} deliveries do canal {Channel} enviadas.", sent, channel);

        return sent;
    }
}
