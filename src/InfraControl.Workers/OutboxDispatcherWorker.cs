using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly int _batchSize = configuration.GetValue("Outbox:BatchSize", 50);
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(
        Math.Max(1, configuration.GetValue("Outbox:PollIntervalSeconds", 5)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox dispatcher started with batch size {BatchSize} and interval {PollInterval}.",
            _batchSize,
            _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatcher batch failed.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var messages = await repository.GetPendingMessagesAsync(_batchSize, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = ResolveEventType(message);
                var payload = JsonSerializer.Deserialize(message.Payload, eventType)
                    ?? throw new InvalidOperationException($"Outbox payload for '{message.EventType}' is empty.");

                await eventBus.PublishAsync(payload, eventType, cancellationToken);
                await repository.MarkAsProcessedAsync(message.Id, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Outbox message {MessageId} of type {EventType} dispatched.",
                    message.Id,
                    message.EventType);
            }
            catch (Exception exception)
            {
                await repository.MarkAsFailedAsync(message.Id, exception.Message, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogError(
                    exception,
                    "Outbox message {MessageId} of type {EventType} failed. Retry {RetryCount}/{MaxRetries}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount,
                    message.MaxRetries);
            }
        }
    }

    private static Type ResolveEventType(OutboxMessage message)
    {
        var eventType = Type.GetType(
            $"InfraControl.Domain.Events.{message.EventType}, InfraControl.Domain",
            throwOnError: false);

        return eventType
            ?? throw new InvalidOperationException($"No domain event type registered for '{message.EventType}'.");
    }
}
