using InfraControl.Application.Messaging;
using InfraControl.Application.Messaging.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers.Messaging;

/// <summary>
/// F55-T010: alimenta os gauges <c>infracontrol_messaging_queue_*</c> com o
/// snapshot real do broker (Management API, poll 30s). Broker fora →
/// snapshot limpo (gauges param de reportar, nunca inventam).
/// </summary>
public sealed class MessagingBrokerMetricsWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MessagingBrokerMetricsWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var broker = scope.ServiceProvider
                    .GetRequiredService<IRabbitMqManagementClient>();
                var queues = await broker.GetQueuesAsync(stoppingToken);

                if (queues.Reachable)
                {
                    MessagingMetrics.BrokerSnapshot.Update(
                        queues.Items.Select(q => new MessagingMetrics.QueueGauge(
                            q.Name, q.MessagesReady, q.MessagesUnacked, q.Consumers))
                            .ToArray());
                }
                else
                {
                    MessagingMetrics.BrokerSnapshot.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                MessagingMetrics.BrokerSnapshot.Clear();
                logger.LogWarning(exception, "Falha ao coletar métricas do broker RabbitMQ.");
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
}
