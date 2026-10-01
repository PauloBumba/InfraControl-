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

    /// <summary>F9-T009: 5 tentativas (30s/60s/120s/240s) — depois, DLQ.</summary>
    public const int MaxAttempts = 5;

    public static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(30);

    protected abstract Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
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
                await DispatchAsync(item, delivery, scope.ServiceProvider, cancellationToken);
                delivery.MarkSent(AppTimeProvider.Current.GetUtcNow().UtcDateTime);
                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                delivery.RegisterFailedAttempt(
                    exception.Message,
                    AppTimeProvider.Current.GetUtcNow().UtcDateTime,
                    MaxAttempts,
                    RetryBackoff);

                if (delivery.NextAttemptAt is null)
                    logger.LogError(
                        "Delivery {DeliveryId} esgotou {MaxAttempts} tentativas no canal {Channel} — DLQ (usuário {UserId}): {Error}.",
                        item.DeliveryId, MaxAttempts, channel, item.UserId, delivery.LastError);
                else
                    logger.LogWarning(
                        exception,
                        "Delivery {DeliveryId} falhou no canal {Channel} (usuário {UserId}) — retry {Attempt}/{MaxAttempts} em {NextAttempt:O}.",
                        item.DeliveryId, channel, item.UserId,
                        delivery.AttemptCount, MaxAttempts, delivery.NextAttemptAt);
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
