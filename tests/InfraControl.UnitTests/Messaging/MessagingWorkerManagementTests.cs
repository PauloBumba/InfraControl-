using FluentAssertions;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Application.Messaging.Workers;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using Moq;

namespace InfraControl.UnitTests.Messaging;

/// <summary>F55-T003: pause/resume broker-level + workers query.</summary>
public sealed class MessagingWorkerManagementTests
{
    private sealed class FakeBroker : IRabbitMqManagementClient
    {
        public List<RabbitMqConsumerInfo> Consumers { get; } = [];
        public bool Reachable { get; set; } = true;
        public List<(string Vhost, string Tag)> Cancelled { get; } = [];
        public List<string> ClosedConnections { get; } = [];

        public Task<RabbitMqBrokerSnapshot> GetOverviewAsync(CancellationToken ct = default)
            => Task.FromResult(RabbitMqBrokerSnapshot.Unreachable("n/a"));
        public Task<RabbitMqListResult<RabbitMqQueueSummary>> GetQueuesAsync(CancellationToken ct = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqQueueSummary>.Unreachable("n/a"));
        public Task<RabbitMqDetailResult<RabbitMqQueueDetail>> GetQueueAsync(string name, CancellationToken ct = default)
            => Task.FromResult(RabbitMqDetailResult<RabbitMqQueueDetail>.Unreachable("n/a"));
        public Task<RabbitMqListResult<RabbitMqExchangeSummary>> GetExchangesAsync(CancellationToken ct = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqExchangeSummary>.Unreachable("n/a"));
        public Task<RabbitMqListResult<RabbitMqBindingSummary>> GetBindingsAsync(CancellationToken ct = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqBindingSummary>.Unreachable("n/a"));
        public Task<RabbitMqListResult<RabbitMqConnectionSummary>> GetConnectionsAsync(CancellationToken ct = default)
            => Task.FromResult(RabbitMqListResult<RabbitMqConnectionSummary>.Unreachable("n/a"));

        public Task<RabbitMqListResult<RabbitMqConsumerInfo>> GetConsumersAsync(CancellationToken ct = default)
            => Task.FromResult(Reachable
                ? RabbitMqListResult<RabbitMqConsumerInfo>.Ok(Consumers)
                : RabbitMqListResult<RabbitMqConsumerInfo>.Unreachable("broker fora"));

        public Task<RabbitMqActionResult> CancelConsumerAsync(
            string vhost, string consumerTag, CancellationToken ct = default)
        {
            Cancelled.Add((vhost, consumerTag));
            Consumers.RemoveAll(c => c.ConsumerTag == consumerTag);
            return Task.FromResult(RabbitMqActionResult.Done());
        }

        public Task<RabbitMqActionResult> CloseConnectionAsync(
            string connectionName, CancellationToken ct = default)
        {
            ClosedConnections.Add(connectionName);
            return Task.FromResult(RabbitMqActionResult.Done());
        }
    }

    private sealed class FakePauseStore : IMessagingPauseStore
    {
        public Dictionary<string, MessagingQueuePause> Pauses { get; } = new();

        public Task<MessagingQueuePause?> GetAsync(string queueName, CancellationToken ct = default)
            => Task.FromResult(Pauses.GetValueOrDefault(queueName));

        public Task<IReadOnlyList<MessagingQueuePause>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MessagingQueuePause>>(
                Pauses.Values.OrderBy(p => p.QueueName).ToArray());

        public Task UpsertAsync(MessagingQueuePause pause, CancellationToken ct = default)
        {
            Pauses[pause.QueueName] = pause;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string queueName, CancellationToken ct = default)
        {
            Pauses.Remove(queueName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocker : IDockerControl
    {
        public List<DockerContainerDto>? Containers { get; set; } = [];

        public Task<IReadOnlyList<DockerContainerDto>?> ListContainersAsync(CancellationToken ct = default)
            => Task.FromResult(Containers is null
                ? null
                : (IReadOnlyList<DockerContainerDto>?)Containers);

        public Task<DockerContainerStats?> GetContainerStatsAsync(string id, CancellationToken ct = default)
            => Task.FromResult<DockerContainerStats?>(new DockerContainerStats(1.5, 100_000));

        public Task<DockerContainerInspectDto?> GetContainerInspectAsync(string id, CancellationToken ct = default)
            => Task.FromResult<DockerContainerInspectDto?>(null);
        public Task<DockerTopologyDto?> GetTopologyAsync(CancellationToken ct = default)
            => Task.FromResult<DockerTopologyDto?>(null);
        public Task<string?> GetContainerLogsAsync(string id, int tail, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task<bool> ControlAsync(string id, DockerContainerAction a, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<DockerContainerUpdateStatus> RemoveContainerAsync(string id, CancellationToken ct = default)
            => Task.FromResult(DockerContainerUpdateStatus.NotFound);
        public Task<DockerContainerUpdateStatus> UpdateResourceLimitsAsync(
            string id, long? mem, double? cpu, CancellationToken ct = default)
            => Task.FromResult(DockerContainerUpdateStatus.NotFound);
    }

    private static RabbitMqConsumerInfo Consumer(
        string queue, string tag, string? conn = "conn-A") => new(
        ConsumerTag: tag, QueueName: queue, Vhost: "/", AckRequired: true,
        PrefetchCount: 24, Active: true, ChannelName: "ch-1",
        ConnectionName: conn, PeerHost: "172.18.0.5");

    private static MessagingQueuePause PauseOf(
        FakePauseStore store, string queue) => store.Pauses[queue];

    [Fact]
    public async Task Pause_CancelsAllConsumers_AndPersistsConnections()
    {
        var broker = new FakeBroker();
        broker.Consumers.Add(Consumer("NotificationEvent", "tag-1", "conn-A"));
        broker.Consumers.Add(Consumer("NotificationEvent", "tag-2", "conn-A"));
        broker.Consumers.Add(Consumer("Outbox", "tag-9", "conn-B"));
        var pauses = new FakePauseStore();
        var handler = new PauseQueueConsumptionCommandHandler(
            broker, pauses, Mock.Of<ICurrentUserContext>());

        var result = await handler.Handle(
            new PauseQueueConsumptionCommand("NotificationEvent", "manutenção"), default);

        result.Success.Should().BeTrue();
        result.ConsumersCancelled.Should().Be(2);
        broker.Cancelled.Should().BeEquivalentTo(
            new[] { ("/", "tag-1"), ("/", "tag-2") });
        broker.Consumers.Should().ContainSingle(c => c.QueueName == "Outbox");
        PauseOf(pauses, "NotificationEvent").GetConnectionNames()
            .Should().BeEquivalentTo(new[] { "conn-A" });
    }

    [Fact]
    public async Task Pause_WithoutConsumers_Fails()
    {
        var broker = new FakeBroker();
        var pauses = new FakePauseStore();
        var handler = new PauseQueueConsumptionCommandHandler(
            broker, pauses, Mock.Of<ICurrentUserContext>());

        var result = await handler.Handle(
            new PauseQueueConsumptionCommand("FilaVazia", null), default);

        result.Success.Should().BeFalse();
        pauses.Pauses.Should().BeEmpty();
    }

    [Fact]
    public async Task Pause_WhenBrokerDown_FailsHonestly()
    {
        var broker = new FakeBroker { Reachable = false };
        var handler = new PauseQueueConsumptionCommandHandler(
            broker, new FakePauseStore(), Mock.Of<ICurrentUserContext>());

        var result = await handler.Handle(
            new PauseQueueConsumptionCommand("Q", null), default);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Broker indisponível");
    }

    [Fact]
    public async Task Resume_ClosesStoredConnections_AndClearsPause()
    {
        var broker = new FakeBroker();
        var pauses = new FakePauseStore();
        pauses.Pauses["NotificationEvent"] = MessagingQueuePause.Create(
            "NotificationEvent", "/", new[] { "conn-A", "conn-B" }, null, null);
        var handler = new ResumeQueueConsumptionCommandHandler(broker, pauses);

        var result = await handler.Handle(
            new ResumeQueueConsumptionCommand("NotificationEvent"), default);

        result.Success.Should().BeTrue();
        result.ConnectionsRecycled.Should().Be(2);
        broker.ClosedConnections.Should().BeEquivalentTo(new[] { "conn-A", "conn-B" });
        pauses.Pauses.Should().BeEmpty();
    }

    [Fact]
    public async Task Resume_WhenNotPaused_Fails()
    {
        var handler = new ResumeQueueConsumptionCommandHandler(
            new FakeBroker(), new FakePauseStore());

        var result = await handler.Handle(
            new ResumeQueueConsumptionCommand("Q"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WorkersQuery_GroupsConsumersByQueue_AndListsContainers()
    {
        var broker = new FakeBroker();
        broker.Consumers.Add(Consumer("NotificationEvent", "t1", "conn-A"));
        broker.Consumers.Add(Consumer("NotificationEvent", "t2", "conn-A"));
        broker.Consumers.Add(Consumer("Outbox", "t3", "conn-B"));
        var docker = new FakeDocker
        {
            Containers =
            [
                new DockerContainerDto("id1", "infracontrol-workers-1", "img", "running",
                    "Up", null, null),
                new DockerContainerDto("id2", "infracontrol-postgres", "pg", "running",
                    "Up", null, null),
            ],
        };
        var pauses = new FakePauseStore();
        pauses.Pauses["Outbox"] = MessagingQueuePause.Create(
            "Outbox", "/", new[] { "conn-B" }, null, null);

        var handler = new GetMessagingWorkersQueryHandler(broker, docker, pauses);
        var result = await handler.Handle(new GetMessagingWorkersQuery(), default);

        result.BrokerReachable.Should().BeTrue();
        result.DockerReachable.Should().BeTrue();
        // 1 container de app (workers) — postgres filtrado — + 2 grupos de fila.
        result.Workers.Should().HaveCount(3);
        result.Workers.Should().Contain(w =>
            w.Kind == "container" && w.Name == "infracontrol-workers-1");
        result.Workers.Should().Contain(w =>
            w.Kind == "consumer-group" && w.ConsumedQueue == "NotificationEvent"
            && w.ConsumerCount == 2 && w.State == "running");
        result.Workers.Should().Contain(w =>
            w.ConsumedQueue == "Outbox" && w.State == "paused"
            && w.PausedAtUtc != null);
    }
}
