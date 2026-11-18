using InfraControl.Application.Messaging.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers.Messaging;

/// <summary>
/// F55-T003 fix: o pause é broker-level (cancel de consumer via Management
/// API), mas qualquer reciclagem de conexão — resume de outra fila, blip de
/// rede, restart do broker — faz o MassTransit re-subscrever TODOS os seus
/// consumers, reabrindo filas pausadas. Este worker re-aplica o pause:
/// a cada ciclo, para cada fila em <c>messaging_queue_pauses</c>, cancela
/// de novo os consumers ativos que reapareceram. Estado de pause é a
/// fonte da verdade; o broker é convergido para ele.
/// </summary>
public sealed class MessagingPauseEnforcerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MessagingPauseEnforcerWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primeira passada após boot + settle do MassTransit.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnforceOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Falha ao re-aplicar pause de filas de mensageria.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EnforceOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var pauses = scope.ServiceProvider
            .GetRequiredService<IMessagingPauseStore>();
        var broker = scope.ServiceProvider
            .GetRequiredService<IRabbitMqManagementClient>();

        var paused = await pauses.ListAsync(cancellationToken);
        if (paused.Count == 0)
            return;

        var consumers = await broker.GetConsumersAsync(cancellationToken);
        if (!consumers.Reachable)
            return; // broker fora — nada a fazer até voltar

        var pausedByQueue = paused
            .GroupBy(p => p.QueueName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var consumer in consumers.Items)
        {
            if (!consumer.Active)
                continue;

            if (!pausedByQueue.TryGetValue(consumer.QueueName, out var pause))
                continue;

            var vhost = string.IsNullOrEmpty(pause.Vhost) ? "/" : pause.Vhost;
            var result = await broker.CancelConsumerAsync(
                vhost, consumer.ConsumerTag, cancellationToken);

            if (result.Success)
                logger.LogInformation(
                    "Pause re-aplicado: consumer {ConsumerTag} da fila {Queue} cancelado de novo.",
                    consumer.ConsumerTag, consumer.QueueName);
            else
                logger.LogWarning(
                    "Pause re-aplicado com falha na fila {Queue} (consumer {ConsumerTag}): {Reason}",
                    consumer.QueueName, consumer.ConsumerTag, result.UnavailableReason);
        }
    }
}
