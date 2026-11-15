using InfraControl.Application.Messaging.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Overview;

/// <summary>
/// Bloco do broker (Management API). <c>Source</c> e <c>Reachable</c> são o
/// badge de fonte do KPI: com broker fora, os campos ficam null e
/// <c>UnavailableReason</c> diz o porquê — nunca número inventado.
/// </summary>
public sealed record BrokerOverviewDto(
    string Source,
    bool Reachable,
    string? UnavailableReason,
    string? ConnectionState,
    int? Exchanges,
    int? Queues,
    int? Consumers,
    int? Connections,
    int? Channels,
    long? MessagesReady,
    long? MessagesUnacked,
    long? MessagesTotal,
    double? PublishRatePerSecond,
    double? DeliverRatePerSecond,
    double? AckRatePerSecond,
    long? PublishedTotal,
    long? DeliveredTotal);

/// <summary>Bloco da aplicação (banco) — sempre real quando há linha.</summary>
public sealed record AppMessagingStatsDto(
    string Source,
    int OutboxPending,
    int OutboxExhausted,
    long OutboxProcessedTotal,
    double? AvgOutboxLatencySeconds,
    long InboxProcessedTotal,
    int DeliveriesPending,
    int DeliveriesProcessing,
    int DeliveriesSent,
    int DeliveriesFailed,
    int DeliveriesRetryDue,
    int DeliveriesDlq,
    int DeliveriesSuppressed,
    int DeliveriesCancelled);

public sealed record MessagingOverviewDto(
    BrokerOverviewDto Broker,
    AppMessagingStatsDto App);

/// <summary>F55-T001 — visão geral da mensageria (dashboard).</summary>
public sealed record GetMessagingOverviewQuery
    : BaseQuery<MessagingOverviewDto>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(15);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingOverviewQueryHandler(
    IRabbitMqManagementClient broker,
    IMessagingStatsStore stats)
    : IRequestHandler<GetMessagingOverviewQuery, MessagingOverviewDto>
{
    public async Task<MessagingOverviewDto> Handle(
        GetMessagingOverviewQuery request, CancellationToken cancellationToken)
    {
        var brokerTask = broker.GetOverviewAsync(cancellationToken);
        var appTask = stats.GetStatsAsync(cancellationToken);
        await Task.WhenAll(brokerTask, appTask);

        var snapshot = await brokerTask;
        var app = await appTask;

        var brokerDto = snapshot.Reachable
            ? new BrokerOverviewDto(
                Source: "rabbitmq-api",
                Reachable: true,
                UnavailableReason: null,
                ConnectionState: snapshot.ConnectionState,
                Exchanges: snapshot.Exchanges,
                Queues: snapshot.Queues,
                Consumers: snapshot.Consumers,
                Connections: snapshot.Connections,
                Channels: snapshot.Channels,
                MessagesReady: snapshot.MessagesReady,
                MessagesUnacked: snapshot.MessagesUnacked,
                MessagesTotal: snapshot.MessagesTotal,
                PublishRatePerSecond: snapshot.PublishRatePerSecond,
                DeliverRatePerSecond: snapshot.DeliverRatePerSecond,
                AckRatePerSecond: snapshot.AckRatePerSecond,
                PublishedTotal: snapshot.PublishedTotal,
                DeliveredTotal: snapshot.DeliveredTotal)
            : new BrokerOverviewDto(
                Source: "rabbitmq-api",
                Reachable: false,
                UnavailableReason: snapshot.UnavailableReason,
                ConnectionState: null,
                Exchanges: null,
                Queues: null,
                Consumers: null,
                Connections: null,
                Channels: null,
                MessagesReady: null,
                MessagesUnacked: null,
                MessagesTotal: null,
                PublishRatePerSecond: null,
                DeliverRatePerSecond: null,
                AckRatePerSecond: null,
                PublishedTotal: null,
                DeliveredTotal: null);

        var appDto = new AppMessagingStatsDto(
            Source: "database",
            OutboxPending: app.OutboxPending,
            OutboxExhausted: app.OutboxExhausted,
            OutboxProcessedTotal: app.OutboxProcessedTotal,
            AvgOutboxLatencySeconds: app.AvgOutboxLatencySeconds,
            InboxProcessedTotal: app.InboxProcessedTotal,
            DeliveriesPending: app.DeliveriesPending,
            DeliveriesProcessing: app.DeliveriesProcessing,
            DeliveriesSent: app.DeliveriesSent,
            DeliveriesFailed: app.DeliveriesFailed,
            DeliveriesRetryDue: app.DeliveriesRetryDue,
            DeliveriesDlq: app.DeliveriesDlq,
            DeliveriesSuppressed: app.DeliveriesSuppressed,
            DeliveriesCancelled: app.DeliveriesCancelled);

        return new MessagingOverviewDto(brokerDto, appDto);
    }
}
