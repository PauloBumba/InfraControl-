using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// F55-T002: listas e detalhe do Explorer (queues/exchanges/bindings/
/// connections) mapeados a partir dos payloads REAIS capturados do broker
/// dev — o contrato com a Management API não pode quebrar em silêncio.
/// </summary>
public sealed class RabbitMqExplorerClientTests
{
    private static string Fixture(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Messaging", "Fixtures", name));

    [Fact]
    public async Task GetQueues_WithRealPayload_ShouldMapSummaries()
    {
        var client = CreateClient(_ => Json(Fixture("rabbitmq-queues.json")));

        var result = await client.GetQueuesAsync();

        result.Reachable.Should().BeTrue();
        result.UnavailableReason.Should().BeNull();
        result.Items.Should().HaveCount(3);

        var dlq = result.Items.Single(q => q.Name == "NotificationEvent_error");
        dlq.Vhost.Should().Be("/");
        dlq.Messages.Should().Be(8);
        dlq.MessagesReady.Should().Be(8);
        dlq.MessagesUnacked.Should().Be(0);
        dlq.Consumers.Should().Be(0);
        dlq.State.Should().Be("running");
        dlq.Durable.Should().BeTrue();

        var worker = result.Items.Single(q => q.Name == "AiInferenceWorker");
        worker.Consumers.Should().Be(1);
        worker.Messages.Should().Be(0);
    }

    [Fact]
    public async Task GetQueue_WithRealPayload_ShouldMapDetailWithRatesAndConsumers()
    {
        var client = CreateClient(_ => Json(Fixture("rabbitmq-queue-detail.json")));

        var result = await client.GetQueueAsync("NotificationEvent");

        result.Reachable.Should().BeTrue();
        result.Item.Should().NotBeNull();
        var detail = result.Item!;
        detail.Name.Should().Be("NotificationEvent");
        detail.Messages.Should().Be(0);
        detail.PublishedTotal.Should().Be(103);
        detail.DeliveredTotal.Should().Be(103);
        detail.PublishRatePerSecond.Should().Be(0);
        detail.ConsumerDetails.Should().ContainSingle();
        var consumer = detail.ConsumerDetails.Single();
        consumer.ConsumerTag.Should().StartWith("amq.ctag-");
        consumer.PrefetchCount.Should().Be(24);
        consumer.Active.Should().BeTrue();
        consumer.Channel.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetQueue_WhenBrokerDoesNotKnowQueue_ShouldReturnNotFound()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetQueueAsync("nao-existe");

        result.Reachable.Should().BeTrue();
        result.Item.Should().BeNull();
        result.UnavailableReason.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "HTTP 503")]
    [InlineData(HttpStatusCode.BadGateway, "HTTP 502")]
    public async Task GetQueues_WithHttpError_ShouldReturnUnreachable(
        HttpStatusCode status, string expectedReason)
    {
        var client = CreateClient(_ => new HttpResponseMessage(status));

        var result = await client.GetQueuesAsync();

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be(expectedReason);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueue_WithHttpError_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetQueueAsync("NotificationEvent");

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be("HTTP 503");
        result.Item.Should().BeNull();
    }

    [Fact]
    public async Task GetExchanges_WithRealPayload_ShouldMapSummaries()
    {
        var client = CreateClient(_ => Json(Fixture("rabbitmq-exchanges.json")));

        var result = await client.GetExchangesAsync();

        result.Reachable.Should().BeTrue();
        result.Items.Should().HaveCount(61);

        var exchange = result.Items.Single(e => e.Name == "NotificationEvent");
        exchange.Type.Should().Be("fanout");
        exchange.Durable.Should().BeTrue();
        exchange.AutoDelete.Should().BeFalse();
        exchange.Internal.Should().BeFalse();
        exchange.Vhost.Should().Be("/");
    }

    [Fact]
    public async Task GetBindings_WithRealPayload_ShouldMapIncludingDefaultExchange()
    {
        var client = CreateClient(_ => Json(Fixture("rabbitmq-bindings.json")));

        var result = await client.GetBindingsAsync();

        result.Reachable.Should().BeTrue();
        result.Items.Should().HaveCount(173);

        var toWorker = result.Items.Single(
            b => b.Destination == "AiInferenceWorker" && b.DestinationType == "queue");
        toWorker.Source.Should().BeEmpty();
        toWorker.RoutingKey.Should().Be("AiInferenceWorker");

        var eventBinding = result.Items.Single(
            b => b.Source == "NotificationEvent" && b.DestinationType == "queue");
        eventBinding.Destination.Should().Be("NotificationEvent");
        eventBinding.RoutingKey.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConnections_WithRealPayload_ShouldMapSummaries()
    {
        var client = CreateClient(_ => Json(Fixture("rabbitmq-connections.json")));

        var result = await client.GetConnectionsAsync();

        result.Reachable.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        var connection = result.Items.Single();
        connection.User.Should().Be("infracontrol");
        connection.State.Should().Be("running");
        connection.Channels.Should().Be(1);
        connection.Name.Should().Contain("->");
        connection.ConnectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExchanges_WhenBrokerDoesNotRespond_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("conn refused"));

        var result = await client.GetExchangesAsync();

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be("não respondeu");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueues_WhenBodyIsHtml_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>gateway</html>", Encoding.UTF8, "text/html"),
        });

        var result = await client.GetQueuesAsync();

        result.Reachable.Should().BeFalse();
        result.UnavailableReason.Should().Be("resposta inválida");
    }

    private static HttpResponseMessage Json(string payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

    private static RabbitMqManagementClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri("http://rabbitmq.test:15672"),
        };
        return new RabbitMqManagementClient(
            http, NullLogger<RabbitMqManagementClient>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(respond(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
