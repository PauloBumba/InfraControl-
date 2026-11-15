using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Messaging.Ports;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace InfraControl.Infrastructure.Messaging;

/// <summary>
/// F55-T001: cliente da Management HTTP API do RabbitMQ (backend-only —
/// o navegador nunca fala com o broker e a credencial fica em env).
/// Falha vira <c>Unreachable</c> honesto (timeout, recusa, HTTP ≠ 2xx),
/// nunca exceção estourando na página; resiliência leve (1 retry + CB)
/// para não amplificar um broker fora do ar a cada poll do dashboard.
/// </summary>
public sealed class RabbitMqManagementClient(
    HttpClient http,
    ILogger<RabbitMqManagementClient> logger) : IRabbitMqManagementClient
{
    public const string HttpClientName = "rabbitmq-management";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public static void ConfigureResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder)
        => builder
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = Failures(),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(45),
                ShouldHandle = Failures(),
            });

    private static PredicateBuilder<HttpResponseMessage> Failures()
        => new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(static response => (int)response.StatusCode >= 500);

    public async Task<RabbitMqBrokerSnapshot> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("api/overview", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return RabbitMqBrokerSnapshot.Unreachable(
                    $"HTTP {(int)response.StatusCode}");
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            var overview = await JsonSerializer
                .DeserializeAsync<OverviewEnvelope>(stream, Json, cancellationToken);
            if (overview is null)
            {
                return RabbitMqBrokerSnapshot.Unreachable("resposta vazia");
            }

            return Map(overview);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RabbitMqBrokerSnapshot.Unreachable("tempo esgotado");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Management API do RabbitMQ indisponível");
            return RabbitMqBrokerSnapshot.Unreachable("não respondeu");
        }
        catch (JsonException ex)
        {
            // Página HTML de proxy/erro com status 2xx: contrato quebrado
            // vira indisponível honesto, nunca exceção na página.
            logger.LogWarning(ex, "Resposta inválida da Management API do RabbitMQ");
            return RabbitMqBrokerSnapshot.Unreachable("resposta inválida");
        }
    }

    public async Task<RabbitMqListResult<RabbitMqQueueSummary>> GetQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        var outcome = await FetchAsync("api/queues", cancellationToken);
        if (outcome.Error is not null)
        {
            return RabbitMqListResult<RabbitMqQueueSummary>.Unreachable(outcome.Error);
        }

        return Parse<QueueJson[]?, RabbitMqListResult<RabbitMqQueueSummary>>(
            outcome.Body!,
            raw => RabbitMqListResult<RabbitMqQueueSummary>.Ok(
                (raw ?? []).Select(MapQueue).ToArray()),
            RabbitMqListResult<RabbitMqQueueSummary>.Unreachable);
    }

    public async Task<RabbitMqDetailResult<RabbitMqQueueDetail>> GetQueueAsync(
        string name, CancellationToken cancellationToken = default)
    {
        var path = $"api/queues/%2F/{Uri.EscapeDataString(name.Trim())}";
        var outcome = await FetchAsync(path, cancellationToken);
        if (outcome.Error is not null)
        {
            return outcome.Error == "HTTP 404"
                ? RabbitMqDetailResult<RabbitMqQueueDetail>.NotFound()
                : RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable(outcome.Error);
        }

        return Parse<QueueJson?, RabbitMqDetailResult<RabbitMqQueueDetail>>(
            outcome.Body!,
            raw => raw is null
                ? RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable("resposta vazia")
                : RabbitMqDetailResult<RabbitMqQueueDetail>.Found(MapQueueDetail(raw)),
            RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable);
    }

    public async Task<RabbitMqListResult<RabbitMqExchangeSummary>> GetExchangesAsync(
        CancellationToken cancellationToken = default)
    {
        var outcome = await FetchAsync("api/exchanges", cancellationToken);
        if (outcome.Error is not null)
        {
            return RabbitMqListResult<RabbitMqExchangeSummary>.Unreachable(outcome.Error);
        }

        return Parse<ExchangeJson[]?, RabbitMqListResult<RabbitMqExchangeSummary>>(
            outcome.Body!,
            raw => RabbitMqListResult<RabbitMqExchangeSummary>.Ok(
                (raw ?? []).Select(MapExchange).ToArray()),
            RabbitMqListResult<RabbitMqExchangeSummary>.Unreachable);
    }

    public async Task<RabbitMqListResult<RabbitMqBindingSummary>> GetBindingsAsync(
        CancellationToken cancellationToken = default)
    {
        var outcome = await FetchAsync("api/bindings", cancellationToken);
        if (outcome.Error is not null)
        {
            return RabbitMqListResult<RabbitMqBindingSummary>.Unreachable(outcome.Error);
        }

        return Parse<BindingJson[]?, RabbitMqListResult<RabbitMqBindingSummary>>(
            outcome.Body!,
            raw => RabbitMqListResult<RabbitMqBindingSummary>.Ok(
                (raw ?? []).Select(MapBinding).ToArray()),
            RabbitMqListResult<RabbitMqBindingSummary>.Unreachable);
    }

    public async Task<RabbitMqListResult<RabbitMqConnectionSummary>> GetConnectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var outcome = await FetchAsync("api/connections", cancellationToken);
        if (outcome.Error is not null)
        {
            return RabbitMqListResult<RabbitMqConnectionSummary>.Unreachable(outcome.Error);
        }

        return Parse<ConnectionJson[]?, RabbitMqListResult<RabbitMqConnectionSummary>>(
            outcome.Body!,
            raw => RabbitMqListResult<RabbitMqConnectionSummary>.Ok(
                (raw ?? []).Select(MapConnection).ToArray()),
            RabbitMqListResult<RabbitMqConnectionSummary>.Unreachable);
    }

    // ---- fetch/parse compartilhados das listas do Explorer (T002) ----------

    private sealed record FetchOutcome(string? Error, string? Body);

    private async Task<FetchOutcome> FetchAsync(
        string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new($"HTTP {(int)response.StatusCode}", null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new(null, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new("tempo esgotado", null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Management API do RabbitMQ indisponível ({Path})", path);
            return new("não respondeu", null);
        }
    }

    private static TResult Parse<TWire, TResult>(
        string body,
        Func<TWire, TResult> map,
        Func<string, TResult> onUnreachable)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<TWire>(body, Json);
            return raw is null
                ? onUnreachable("resposta vazia")
                : map(raw);
        }
        catch (JsonException)
        {
            return onUnreachable("resposta inválida");
        }
    }

    private static RabbitMqQueueSummary MapQueue(QueueJson queue) => new(
        Name: queue.Name ?? string.Empty,
        Vhost: queue.Vhost ?? "/",
        Messages: queue.Messages ?? 0,
        MessagesReady: queue.MessagesReady ?? 0,
        MessagesUnacked: queue.MessagesUnacknowledged ?? 0,
        Consumers: queue.Consumers ?? 0,
        State: queue.State ?? "unknown",
        Durable: queue.Durable ?? false);

    private static RabbitMqQueueDetail MapQueueDetail(QueueJson queue)
    {
        var stats = queue.MessageStats;
        var consumers = (queue.ConsumerDetails ?? [])
            .Select(c => new RabbitMqQueueConsumer(
                ConsumerTag: c.ConsumerTag ?? string.Empty,
                PrefetchCount: c.PrefetchCount,
                Active: c.Active,
                Channel: c.ChannelDetails?.Name))
            .ToArray();

        return new RabbitMqQueueDetail(
            Name: queue.Name ?? string.Empty,
            Vhost: queue.Vhost ?? "/",
            Messages: queue.Messages ?? 0,
            MessagesReady: queue.MessagesReady ?? 0,
            MessagesUnacked: queue.MessagesUnacknowledged ?? 0,
            Consumers: queue.Consumers ?? 0,
            State: queue.State ?? "unknown",
            Durable: queue.Durable ?? false,
            PublishRatePerSecond: stats?.PublishDetails?.Rate,
            DeliverRatePerSecond: stats?.DeliverDetails?.Rate,
            AckRatePerSecond: stats?.AckDetails?.Rate,
            RedeliverRatePerSecond: stats?.RedeliverDetails?.Rate,
            PublishedTotal: stats?.Publish ?? 0,
            DeliveredTotal: stats?.Deliver ?? 0,
            ConsumerDetails: consumers);
    }

    private static RabbitMqExchangeSummary MapExchange(ExchangeJson exchange) => new(
        Name: exchange.Name ?? string.Empty,
        Vhost: exchange.Vhost ?? "/",
        Type: exchange.Type ?? "unknown",
        Durable: exchange.Durable ?? false,
        AutoDelete: exchange.AutoDelete ?? false,
        Internal: exchange.Internal ?? false,
        PublishedTotal: exchange.MessageStats?.Publish,
        PublishRatePerSecond: exchange.MessageStats?.PublishDetails?.Rate);

    private static RabbitMqBindingSummary MapBinding(BindingJson binding) => new(
        Source: binding.Source ?? string.Empty,
        Destination: binding.Destination ?? string.Empty,
        DestinationType: binding.DestinationType ?? string.Empty,
        RoutingKey: binding.RoutingKey ?? string.Empty);

    private static RabbitMqConnectionSummary MapConnection(ConnectionJson connection)
    {
        // connected_at: segundos em brokers antigos, milissegundos nos atuais
        // (payload real do dev veio em 13 dígitos).
        DateTime? connectedAt = connection.ConnectedAt is null
            ? null
            : connection.ConnectedAt > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(connection.ConnectedAt.Value)
                    .UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(connection.ConnectedAt.Value)
                    .UtcDateTime;

        return new RabbitMqConnectionSummary(
            Name: connection.Name ?? string.Empty,
            User: connection.User ?? string.Empty,
            State: connection.State ?? "unknown",
            Channels: connection.Channels ?? 0,
            ConnectedAt: connectedAt);
    }

    // ---- wire records (JSON da Management API) -----------------------------

    private sealed record QueueJson(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("vhost")] string? Vhost,
        [property: JsonPropertyName("messages")] long? Messages,
        [property: JsonPropertyName("messages_ready")] long? MessagesReady,
        [property: JsonPropertyName("messages_unacknowledged")] long? MessagesUnacknowledged,
        [property: JsonPropertyName("consumers")] int? Consumers,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("durable")] bool? Durable,
        [property: JsonPropertyName("message_stats")] QueueMessageStats? MessageStats,
        [property: JsonPropertyName("consumer_details")] ConsumerJson[]? ConsumerDetails);

    private sealed record QueueMessageStats(
        [property: JsonPropertyName("publish")] long? Publish,
        [property: JsonPropertyName("deliver")] long? Deliver,
        [property: JsonPropertyName("ack")] long? Ack,
        [property: JsonPropertyName("publish_details")] RateDetails? PublishDetails,
        [property: JsonPropertyName("deliver_details")] RateDetails? DeliverDetails,
        [property: JsonPropertyName("ack_details")] RateDetails? AckDetails,
        [property: JsonPropertyName("redeliver_details")] RateDetails? RedeliverDetails);

    private sealed record ConsumerJson(
        [property: JsonPropertyName("consumer_tag")] string? ConsumerTag,
        [property: JsonPropertyName("prefetch_count")] int? PrefetchCount,
        [property: JsonPropertyName("active")] bool? Active,
        [property: JsonPropertyName("channel_details")] ChannelJson? ChannelDetails);

    private sealed record ChannelJson(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record ExchangeJson(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("vhost")] string? Vhost,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("durable")] bool? Durable,
        [property: JsonPropertyName("auto_delete")] bool? AutoDelete,
        [property: JsonPropertyName("internal")] bool? Internal,
        [property: JsonPropertyName("message_stats")] ExchangeMessageStats? MessageStats);

    private sealed record ExchangeMessageStats(
        [property: JsonPropertyName("publish")] long? Publish,
        [property: JsonPropertyName("publish_details")] RateDetails? PublishDetails);

    private sealed record BindingJson(
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("destination")] string? Destination,
        [property: JsonPropertyName("destination_type")] string? DestinationType,
        [property: JsonPropertyName("routing_key")] string? RoutingKey);

    private sealed record ConnectionJson(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("user")] string? User,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("channels")] int? Channels,
        [property: JsonPropertyName("connected_at")] long? ConnectedAt);

    private static RabbitMqBrokerSnapshot Map(OverviewEnvelope overview)
    {
        var totals = overview.ObjectTotals;
        var queueTotals = overview.QueueTotals;
        var stats = overview.MessageStats;

        return new RabbitMqBrokerSnapshot(
            Reachable: true,
            UnavailableReason: null,
            // O management responder 2xx = broker vivo para a API de gestão.
            ConnectionState: "running",
            Exchanges: totals?.Exchanges ?? 0,
            Queues: totals?.Queues ?? 0,
            Consumers: totals?.Consumers ?? 0,
            Connections: totals?.Connections ?? 0,
            Channels: totals?.Channels ?? 0,
            MessagesReady: queueTotals?.MessagesReady ?? 0,
            MessagesUnacked: queueTotals?.MessagesUnacknowledged ?? 0,
            MessagesTotal: queueTotals?.Messages ?? 0,
            PublishRatePerSecond: stats?.PublishDetails?.Rate,
            DeliverRatePerSecond: stats?.DeliverDetails?.Rate,
            AckRatePerSecond: stats?.AckDetails?.Rate,
            PublishedTotal: stats?.Publish ?? 0,
            DeliveredTotal: stats?.Deliver ?? 0);
    }

    private sealed record OverviewEnvelope(
        [property: JsonPropertyName("object_totals")] ObjectTotals? ObjectTotals,
        [property: JsonPropertyName("queue_totals")] QueueTotals? QueueTotals,
        [property: JsonPropertyName("message_stats")] MessageStats? MessageStats);

    private sealed record ObjectTotals(
        [property: JsonPropertyName("queues")] int? Queues,
        [property: JsonPropertyName("connections")] int? Connections,
        [property: JsonPropertyName("consumers")] int? Consumers,
        [property: JsonPropertyName("exchanges")] int? Exchanges,
        [property: JsonPropertyName("channels")] int? Channels);

    private sealed record QueueTotals(
        [property: JsonPropertyName("messages")] long? Messages,
        [property: JsonPropertyName("messages_ready")] long? MessagesReady,
        [property: JsonPropertyName("messages_unacknowledged")] long? MessagesUnacknowledged);

    private sealed record MessageStats(
        [property: JsonPropertyName("publish")] long? Publish,
        [property: JsonPropertyName("deliver")] long? Deliver,
        [property: JsonPropertyName("ack")] long? Ack,
        [property: JsonPropertyName("publish_details")] RateDetails? PublishDetails,
        [property: JsonPropertyName("deliver_details")] RateDetails? DeliverDetails,
        [property: JsonPropertyName("ack_details")] RateDetails? AckDetails);

    private sealed record RateDetails(
        [property: JsonPropertyName("rate")] double? Rate);
}
