using FluentAssertions;
using InfraControl.Application.Messaging.Explorer;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Access;
using InfraControl.Domain.Enums;
using Xunit;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// F55-T002: queries do Queue/Exchange/Binding/Connection Explorer —
/// leitura Operate, cache curto com tag `messaging` e indisponível
/// honesto quando o broker não responde (nunca lista vazia "normal").
/// </summary>
public sealed class MessagingExplorerQueriesTests
{
    private sealed class FakeClient : IRabbitMqManagementClient
    {
        public RabbitMqBrokerSnapshot Overview { get; init; } =
            RabbitMqBrokerSnapshot.Unreachable("não usado");

        public RabbitMqListResult<RabbitMqQueueSummary> Queues { get; init; } =
            RabbitMqListResult<RabbitMqQueueSummary>.Ok([]);

        public RabbitMqDetailResult<RabbitMqQueueDetail> QueueDetail { get; init; } =
            RabbitMqDetailResult<RabbitMqQueueDetail>.NotFound();

        public RabbitMqListResult<RabbitMqExchangeSummary> Exchanges { get; init; } =
            RabbitMqListResult<RabbitMqExchangeSummary>.Ok([]);

        public RabbitMqListResult<RabbitMqBindingSummary> Bindings { get; init; } =
            RabbitMqListResult<RabbitMqBindingSummary>.Ok([]);

        public RabbitMqListResult<RabbitMqConnectionSummary> Connections { get; init; } =
            RabbitMqListResult<RabbitMqConnectionSummary>.Ok([]);

        public Task<RabbitMqBrokerSnapshot> GetOverviewAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Overview);

        public Task<RabbitMqListResult<RabbitMqQueueSummary>> GetQueuesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Queues);

        public Task<RabbitMqDetailResult<RabbitMqQueueDetail>> GetQueueAsync(
            string name, CancellationToken cancellationToken = default)
            => Task.FromResult(QueueDetail);

        public Task<RabbitMqListResult<RabbitMqExchangeSummary>> GetExchangesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Exchanges);

        public Task<RabbitMqListResult<RabbitMqBindingSummary>> GetBindingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Bindings);

        public Task<RabbitMqListResult<RabbitMqConnectionSummary>> GetConnectionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Connections);
    }

    private static RabbitMqQueueSummary Queue(
        string name, long messages = 0, int consumers = 0) => new(
        Name: name,
        Vhost: "/",
        Messages: messages,
        MessagesReady: messages,
        MessagesUnacked: 0,
        Consumers: consumers,
        State: "running",
        Durable: true);

    [Fact]
    public void ListQueries_ShouldRequireOperate_AndDeclareMessagingCache()
    {
        new GetMessagingQueuesQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        new GetMessagingQueuesQuery().CacheTags.Should().Contain("messaging");
        new GetMessagingQueuesQuery().CacheDuration.Should().Be(TimeSpan.FromSeconds(10));

        new GetMessagingExchangesQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        new GetMessagingBindingsQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        new GetMessagingConnectionsQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        new GetMessagingExchangesQuery().CacheTags.Should().Contain("messaging");
        new GetMessagingConnectionsQuery().CacheTags.Should().Contain("messaging");
    }

    [Fact]
    public void DetailQuery_ShouldRequireOperate_AndPutNameInCacheKey()
    {
        var query = new GetMessagingQueueDetailQuery("NotificationEvent");

        query.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        query.CacheTags.Should().Contain("messaging");
        query.CacheKey.Should().Contain("NotificationEvent");
        query.CacheKey.Should().NotBe(new GetMessagingQueueDetailQuery("Outra").CacheKey);
    }

    [Fact]
    public async Task HandleQueues_WhenReachable_ShouldReturnItems()
    {
        var handler = new GetMessagingQueuesQueryHandler(new FakeClient
        {
            Queues = RabbitMqListResult<RabbitMqQueueSummary>.Ok(
                [Queue("NotificationEvent", messages: 8), Queue("AiInferenceWorker", consumers: 1)]),
        });

        var result = await handler.Handle(
            new GetMessagingQueuesQuery(), CancellationToken.None);

        result.Reachable.Should().BeTrue();
        result.UnavailableReason.Should().BeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Single(q => q.Name == "NotificationEvent").Messages.Should().Be(8);
    }

    [Fact]
    public async Task HandleQueues_WhenBrokerUnreachable_ShouldKeepHonestReason()
    {
        var handler = new GetMessagingQueuesQueryHandler(new FakeClient
        {
            Queues = RabbitMqListResult<RabbitMqQueueSummary>.Unreachable("HTTP 503"),
        });

        var result = await handler.Handle(
            new GetMessagingQueuesQuery(), CancellationToken.None);

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be("HTTP 503");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleQueueDetail_WhenNotFound_ShouldBeReachableWithNullItem()
    {
        var handler = new GetMessagingQueueDetailQueryHandler(new FakeClient
        {
            QueueDetail = RabbitMqDetailResult<RabbitMqQueueDetail>.NotFound(),
        });

        var result = await handler.Handle(
            new GetMessagingQueueDetailQuery("sumiu"), CancellationToken.None);

        result.Reachable.Should().BeTrue();
        result.Item.Should().BeNull();
        result.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task HandleQueueDetail_WhenUnreachable_ShouldCarryReason()
    {
        var handler = new GetMessagingQueueDetailQueryHandler(new FakeClient
        {
            QueueDetail = RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable("tempo esgotado"),
        });

        var result = await handler.Handle(
            new GetMessagingQueueDetailQuery("NotificationEvent"), CancellationToken.None);

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be("tempo esgotado");
        result.Item.Should().BeNull();
    }

    [Fact]
    public async Task HandleExchanges_AndConnections_ShouldDelegateToClient()
    {
        var exchanges = RabbitMqListResult<RabbitMqExchangeSummary>.Ok(
        [
            new RabbitMqExchangeSummary(
                Name: "NotificationEvent", Vhost: "/", Type: "fanout",
                Durable: true, AutoDelete: false, Internal: false,
                PublishedTotal: null, PublishRatePerSecond: null),
        ]);
        var connections = RabbitMqListResult<RabbitMqConnectionSummary>.Ok(
        [
            new RabbitMqConnectionSummary(
                Name: "172.30.0.2:1000 -> 172.30.0.3:5672",
                User: "infracontrol",
                State: "running",
                Channels: 2,
                ConnectedAt: DateTime.UtcNow),
        ]);
        var client = new FakeClient
        {
            Exchanges = exchanges,
            Connections = connections,
        };

        var exchangeResult = await new GetMessagingExchangesQueryHandler(client)
            .Handle(new GetMessagingExchangesQuery(), CancellationToken.None);
        var connectionResult = await new GetMessagingConnectionsQueryHandler(client)
            .Handle(new GetMessagingConnectionsQuery(), CancellationToken.None);

        exchangeResult.Items.Single().Name.Should().Be("NotificationEvent");
        connectionResult.Items.Single().User.Should().Be("infracontrol");
        connectionResult.Items.Single().Channels.Should().Be(2);
    }
}
