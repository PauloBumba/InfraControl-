namespace InfraControl.Application.Messaging.Ports;

/// <summary>
/// F55-T001: instantâneo do broker lido na Management HTTP API
/// (`GET /api/overview`). Campos numéricos só têm sentido quando
/// <see cref="Reachable"/> — indisponível é honesto, nunca inventado.
/// </summary>
public sealed record RabbitMqBrokerSnapshot(
    bool Reachable,
    string? UnavailableReason,
    string? ConnectionState,
    int Exchanges,
    int Queues,
    int Consumers,
    int Connections,
    int Channels,
    long MessagesReady,
    long MessagesUnacked,
    long MessagesTotal,
    double? PublishRatePerSecond,
    double? DeliverRatePerSecond,
    double? AckRatePerSecond,
    long PublishedTotal,
    long DeliveredTotal)
{
    public static RabbitMqBrokerSnapshot Unreachable(string reason) => new(
        Reachable: false,
        UnavailableReason: reason,
        ConnectionState: null,
        Exchanges: 0,
        Queues: 0,
        Consumers: 0,
        Connections: 0,
        Channels: 0,
        MessagesReady: 0,
        MessagesUnacked: 0,
        MessagesTotal: 0,
        PublishRatePerSecond: null,
        DeliverRatePerSecond: null,
        AckRatePerSecond: null,
        PublishedTotal: 0,
        DeliveredTotal: 0);
}

/// <summary>
/// Porta do cliente da Management API do RabbitMQ (F55-T001). O navegador
/// nunca fala com o broker: só o backend, com credenciais de env.
/// </summary>
public interface IRabbitMqManagementClient
{
    Task<RabbitMqBrokerSnapshot> GetOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<RabbitMqListResult<RabbitMqQueueSummary>> GetQueuesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detalhe de fila. <c>Reachable=true</c> com <c>Item=null</c> = o broker
    /// respondeu e não conhece a fila (404); <c>Reachable=false</c> = broker
    /// indisponível (a lista não pode fingir "não existe").
    /// </summary>
    Task<RabbitMqDetailResult<RabbitMqQueueDetail>> GetQueueAsync(
        string name, CancellationToken cancellationToken = default);

    Task<RabbitMqListResult<RabbitMqExchangeSummary>> GetExchangesAsync(
        CancellationToken cancellationToken = default);

    Task<RabbitMqListResult<RabbitMqBindingSummary>> GetBindingsAsync(
        CancellationToken cancellationToken = default);

    Task<RabbitMqListResult<RabbitMqConnectionSummary>> GetConnectionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F55-T003: consumers ativos do broker (GET /api/consumers) — base do
    /// Worker Management e do pause broker-level.
    /// </summary>
    Task<RabbitMqListResult<RabbitMqConsumerInfo>> GetConsumersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela um consumer (DELETE /api/consumers/{vhost}/{tag}) — base do
    /// pause broker-level. O app não é notificado; o resume recicla a
    /// conexão para o cliente re-subscrever.
    /// </summary>
    Task<RabbitMqActionResult> CancelConsumerAsync(
        string vhost, string consumerTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fecha uma conexão (DELETE /api/connections/{name}) — força o cliente
    /// (MassTransit) a reconectar e re-subscrever consumers. Usado no resume.
    /// </summary>
    Task<RabbitMqActionResult> CloseConnectionAsync(
        string connectionName, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de uma ação administrativa no broker (nunca lança).</summary>
public sealed record RabbitMqActionResult(
    bool Reachable,
    string? UnavailableReason,
    bool Success,
    string? Detail)
{
    public static RabbitMqActionResult Done(string? detail = null)
        => new(true, null, true, detail);

    public static RabbitMqActionResult Failed(string reason)
        => new(false, reason, false, reason);
}

/// <summary>Consumer ativo visto pela Management API (GET /api/consumers).</summary>
public sealed record RabbitMqConsumerInfo(
    string ConsumerTag,
    string QueueName,
    string Vhost,
    bool AckRequired,
    int PrefetchCount,
    bool Active,
    string? ChannelName,
    string? ConnectionName,
    string? PeerHost);

/// <summary>
/// Lista do Explorer com disponibilidade explícita: <c>Reachable=false</c> +
/// motivo = broker fora (lista vazia ≠ "nada existe").
/// </summary>
public sealed record RabbitMqListResult<T>(
    bool Reachable,
    string? UnavailableReason,
    IReadOnlyList<T> Items)
{
    public static RabbitMqListResult<T> Ok(IReadOnlyList<T> items)
        => new(true, null, items);

    public static RabbitMqListResult<T> Unreachable(string reason)
        => new(false, reason, []);
}

/// <summary>Detalhe único — distingue 404 (fila não existe) de broker fora.</summary>
public sealed record RabbitMqDetailResult<T>(
    bool Reachable,
    string? UnavailableReason,
    T? Item)
{
    public static RabbitMqDetailResult<T> Found(T item)
        => new(true, null, item);

    public static RabbitMqDetailResult<T> NotFound()
        => new(true, null, default);

    public static RabbitMqDetailResult<T> Unreachable(string reason)
        => new(false, reason, default);
}

public sealed record RabbitMqQueueSummary(
    string Name,
    string Vhost,
    long Messages,
    long MessagesReady,
    long MessagesUnacked,
    int Consumers,
    string State,
    bool Durable);

public sealed record RabbitMqQueueConsumer(
    string ConsumerTag,
    int? PrefetchCount,
    bool? Active,
    string? Channel);

public sealed record RabbitMqQueueDetail(
    string Name,
    string Vhost,
    long Messages,
    long MessagesReady,
    long MessagesUnacked,
    int Consumers,
    string State,
    bool Durable,
    double? PublishRatePerSecond,
    double? DeliverRatePerSecond,
    double? AckRatePerSecond,
    double? RedeliverRatePerSecond,
    long PublishedTotal,
    long DeliveredTotal,
    IReadOnlyList<RabbitMqQueueConsumer> ConsumerDetails);

public sealed record RabbitMqExchangeSummary(
    string Name,
    string Vhost,
    string Type,
    bool Durable,
    bool AutoDelete,
    bool Internal,
    long? PublishedTotal,
    double? PublishRatePerSecond);

public sealed record RabbitMqBindingSummary(
    string Source,
    string Destination,
    string DestinationType,
    string RoutingKey);

public sealed record RabbitMqConnectionSummary(
    string Name,
    string User,
    string State,
    int Channels,
    DateTime? ConnectedAt);
