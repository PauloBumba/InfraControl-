using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// F55-T001: contrato do cliente da Management API do RabbitMQ.
/// O fixture é o payload REAL capturado do broker dev (T001) — garante que
/// o mapeamento dos campos que a Overview consome não quebra silenciosamente.
/// </summary>
public sealed class RabbitMqManagementClientTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Messaging", "Fixtures", "rabbitmq-overview.json");

    [Fact]
    public async Task GetOverview_WithRealBrokerPayload_ShouldMapTotals()
    {
        var payload = await File.ReadAllTextAsync(FixturePath);
        var client = CreateClient(_ => Json(payload));

        var snapshot = await client.GetOverviewAsync();

        snapshot.Reachable.Should().BeTrue();
        snapshot.UnavailableReason.Should().BeNull();
        snapshot.ConnectionState.Should().Be("running");
        snapshot.Exchanges.Should().Be(61);
        snapshot.Queues.Should().Be(3);
        snapshot.Consumers.Should().Be(2);
        snapshot.Connections.Should().Be(2);
        snapshot.Channels.Should().Be(4);
        snapshot.MessagesTotal.Should().Be(8);
        snapshot.MessagesReady.Should().Be(8);
        snapshot.MessagesUnacked.Should().Be(0);
        snapshot.PublishedTotal.Should().Be(359);
        snapshot.DeliveredTotal.Should().Be(93);
        snapshot.AckRatePerSecond.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "HTTP 503")]
    [InlineData(HttpStatusCode.Unauthorized, "HTTP 401")]
    [InlineData(HttpStatusCode.NotFound, "HTTP 404")]
    public async Task GetOverview_WithHttpError_ShouldReturnUnreachable(
        HttpStatusCode status, string expectedReason)
    {
        var client = CreateClient(_ => new HttpResponseMessage(status));

        var snapshot = await client.GetOverviewAsync();

        snapshot.Reachable.Should().BeFalse();
        snapshot.UnavailableReason.Should().Be(expectedReason);
        snapshot.Exchanges.Should().Be(0);
    }

    [Fact]
    public async Task GetOverview_WhenBrokerDoesNotRespond_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("conn refused"));

        var snapshot = await client.GetOverviewAsync();

        snapshot.Reachable.Should().BeFalse();
        snapshot.UnavailableReason.Should().Be("não respondeu");
    }

    [Fact]
    public async Task GetOverview_WhenRequestTimesOut_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => throw new TaskCanceledException());

        var snapshot = await client.GetOverviewAsync();

        snapshot.Reachable.Should().BeFalse();
        snapshot.UnavailableReason.Should().Be("tempo esgotado");
    }

    [Fact]
    public async Task GetOverview_WithNonJsonBody_ShouldReturnUnreachable()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>gateway</html>", Encoding.UTF8, "text/html"),
        });

        var snapshot = await client.GetOverviewAsync();

        snapshot.Reachable.Should().BeFalse();
        snapshot.UnavailableReason.Should().Be("resposta inválida");
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
