using FluentAssertions;
using InfraControl.Application.Messaging.Overview;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Access;
using InfraControl.Domain.Enums;
using Xunit;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// F55-T001: overview da mensageria — agregação broker (Management API) ×
/// banco (outbox/inbox/deliveries) com fonte explícita por bloco e
/// indisponível honesto quando o broker não responde.
/// </summary>
public sealed class MessagingOverviewQueryTests
{
    private sealed class FakeBrokerClient(RabbitMqBrokerSnapshot snapshot)
        : IRabbitMqManagementClient
    {
        public Task<RabbitMqBrokerSnapshot> GetOverviewAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);

        public Task<RabbitMqListResult<RabbitMqQueueSummary>> GetQueuesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqQueueSummary>.Unreachable("não configurado"));

        public Task<RabbitMqDetailResult<RabbitMqQueueDetail>> GetQueueAsync(
            string name, CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable("não configurado"));

        public Task<RabbitMqListResult<RabbitMqExchangeSummary>> GetExchangesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqExchangeSummary>.Unreachable("não configurado"));

        public Task<RabbitMqListResult<RabbitMqBindingSummary>> GetBindingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqBindingSummary>.Unreachable("não configurado"));

        public Task<RabbitMqListResult<RabbitMqConnectionSummary>> GetConnectionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqConnectionSummary>.Unreachable("não configurado"));

        public Task<RabbitMqListResult<RabbitMqConsumerInfo>> GetConsumersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqConsumerInfo>.Unreachable("não configurado"));

        public Task<RabbitMqActionResult> CancelConsumerAsync(
            string vhost, string consumerTag, CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqActionResult.Failed("não configurado"));

        public Task<RabbitMqActionResult> CloseConnectionAsync(
            string connectionName, CancellationToken cancellationToken = default)
            => Task.FromResult(RabbitMqActionResult.Failed("não configurado"));
    }

    private sealed class FakeStatsStore(MessagingAppStats stats) : IMessagingStatsStore
    {
        public Task<MessagingAppStats> GetStatsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(stats);
    }

    private static RabbitMqBrokerSnapshot ReachableSnapshot() => new(
        Reachable: true,
        UnavailableReason: null,
        ConnectionState: "running",
        Exchanges: 61,
        Queues: 3,
        Consumers: 2,
        Connections: 2,
        Channels: 4,
        MessagesReady: 8,
        MessagesUnacked: 0,
        MessagesTotal: 8,
        PublishRatePerSecond: 1.5,
        DeliverRatePerSecond: 1.2,
        AckRatePerSecond: 1.1,
        PublishedTotal: 120,
        DeliveredTotal: 118);

    private static MessagingAppStats AppStats() => new(
        OutboxPending: 4,
        OutboxExhausted: 1,
        OutboxProcessedTotal: 950,
        AvgOutboxLatencySeconds: 0.42,
        InboxProcessedTotal: 940,
        DeliveriesPending: 7,
        DeliveriesProcessing: 1,
        DeliveriesSent: 500,
        DeliveriesFailed: 12,
        DeliveriesRetryDue: 5,
        DeliveriesDlq: 3,
        DeliveriesSuppressed: 2,
        DeliveriesCancelled: 1);

    [Fact]
    public void Query_ShouldRequireOperate_AndDeclareMessagingCache()
    {
        var query = new GetMessagingOverviewQuery();

        query.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        query.CacheTags.Should().Contain("messaging");
        query.CacheDuration.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Handle_ShouldAggregateBrokerAndAppStats_WithExplicitSources()
    {
        var handler = new GetMessagingOverviewQueryHandler(
            new FakeBrokerClient(ReachableSnapshot()),
            new FakeStatsStore(AppStats()));

        var overview = await handler.Handle(
            new GetMessagingOverviewQuery(), CancellationToken.None);

        overview.Broker.Source.Should().Be("rabbitmq-api");
        overview.Broker.Reachable.Should().BeTrue();
        overview.Broker.ConnectionState.Should().Be("running");
        overview.Broker.Exchanges.Should().Be(61);
        overview.Broker.Queues.Should().Be(3);
        overview.Broker.Consumers.Should().Be(2);
        overview.Broker.MessagesReady.Should().Be(8);
        overview.Broker.PublishRatePerSecond.Should().Be(1.5);
        overview.Broker.PublishedTotal.Should().Be(120);

        overview.App.Source.Should().Be("database");
        overview.App.OutboxPending.Should().Be(4);
        overview.App.OutboxExhausted.Should().Be(1);
        overview.App.InboxProcessedTotal.Should().Be(940);
        overview.App.AvgOutboxLatencySeconds.Should().Be(0.42);
        overview.App.DeliveriesRetryDue.Should().Be(5);
        overview.App.DeliveriesDlq.Should().Be(3);
        overview.App.DeliveriesSent.Should().Be(500);
    }

    [Fact]
    public async Task Handle_WhenBrokerUnreachable_ShouldReturnHonestUnavailable_AndKeepAppStats()
    {
        var handler = new GetMessagingOverviewQueryHandler(
            new FakeBrokerClient(RabbitMqBrokerSnapshot.Unreachable("o broker não respondeu")),
            new FakeStatsStore(AppStats()));

        var overview = await handler.Handle(
            new GetMessagingOverviewQuery(), CancellationToken.None);

        overview.Broker.Reachable.Should().BeFalse();
        overview.Broker.UnavailableReason.Should().Be("o broker não respondeu");
        overview.Broker.ConnectionState.Should().BeNull();
        overview.Broker.Exchanges.Should().BeNull();
        overview.Broker.MessagesReady.Should().BeNull();
        overview.Broker.PublishRatePerSecond.Should().BeNull();

        // O bloco do banco continua real — indisponível só onde não há dado.
        overview.App.Source.Should().Be("database");
        overview.App.OutboxPending.Should().Be(4);
    }

    [Fact]
    public void AccessModuleCatalog_ShouldContainMessagingModule()
    {
        AccessModuleCatalog.FromRouteSegment("messaging").Should().Be("messaging");
        AccessModuleCatalog.LabelOf("messaging").Should().NotBeNullOrWhiteSpace();
        AccessModuleCatalog
            .FromRequestNamespace("InfraControl.Application.Messaging.Overview")
            .Should().Be("messaging");
    }
}
