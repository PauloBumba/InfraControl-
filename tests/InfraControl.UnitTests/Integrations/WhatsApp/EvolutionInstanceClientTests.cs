using System.Net;
using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Infrastructure.Integrations.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations.WhatsApp;

public sealed class EvolutionInstanceClientTests
{
    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private static (EvolutionInstanceClient Client, List<HttpRequestMessage> Requests) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var requests = new List<HttpRequestMessage>();
        var messageHandler = new DelegatingHandlerStub(req =>
        {
            requests.Add(req);
            return handler(req);
        });

        var httpClient = new HttpClient(messageHandler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(EvolutionInstanceClient.HttpClientName))
            .Returns(httpClient);

        var client = new EvolutionInstanceClient(factory.Object, NullLogger<EvolutionInstanceClient>.Instance);
        return (client, requests);
    }

    private static WhatsAppChannelOptions ValidOptions => new()
    {
        BaseUrl = "http://evolution:8080",
        ApiKey = "secret-api-key",
        InstanceId = "Admin"
    };

    [Fact]
    public async Task GetConnectionStateAsync_ShouldSendGetWithApiKeyAndReturnState()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/instance/connectionState/Admin");
            req.Headers.GetValues("apikey").Should().ContainSingle("secret-api-key");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"instance\":{\"state\":\"open\"}}")
            };
        });

        var state = await client.GetConnectionStateAsync(ValidOptions, CancellationToken.None);

        state.Should().Be("open");
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ConnectAsync_ShouldSendGetWithApiKeyAndReturnQrResult()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/instance/connect/Admin");
            req.Headers.GetValues("apikey").Should().ContainSingle("secret-api-key");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"base64\":\"data:image/png;base64,QUJD\",\"pairingCode\":\"1234-5678\"}")
            };
        });

        var qr = await client.ConnectAsync(ValidOptions, CancellationToken.None);

        qr.Base64.Should().Be("data:image/png;base64,QUJD");
        qr.PairingCode.Should().Be("1234-5678");
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task LogoutAsync_When400Returned_ShouldNotThrow()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Delete);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/instance/logout/Admin");
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var act = () => client.LogoutAsync(ValidOptions, CancellationToken.None);

        await act.Should().NotThrowAsync();
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInstanceAsync_ShouldSendPostToBodyEndpoint()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/instance/create");
            req.Headers.GetValues("apikey").Should().ContainSingle("secret-api-key");
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var result = await client.CreateInstanceAsync(ValidOptions, CancellationToken.None);

        result.Created.Should().BeTrue();
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInstanceAsync_WhenConflictReturned_ShouldReportExistingInstance()
    {
        var (client, requests) = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.Conflict));

        var result = await client.CreateInstanceAsync(ValidOptions, CancellationToken.None);

        result.Created.Should().BeFalse();
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInstanceAsync_WhenBadRequestReturned_ShouldThrow()
    {
        var (client, requests) = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => client.CreateInstanceAsync(ValidOptions, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ListGroupsAsync_ShouldSendGetAndReturnGroups()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/group/fetchAllGroups/Admin?getParticipants=false");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":\"123@g.us\",\"subject\":\"DevOps\",\"size\":15,\"restrict\":false,\"announce\":false}]")
            };
        });

        var groups = await client.ListGroupsAsync(ValidOptions, CancellationToken.None);

        groups.Should().ContainSingle();
        groups[0].Id.Should().Be("123@g.us");
        groups[0].Subject.Should().Be("DevOps");
        groups[0].Size.Should().Be(15);
    }

    [Fact]
    public async Task SendTextAsync_ShouldSendPostWithNumberAndText()
    {
        var (client, requests) = CreateClient(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Be("http://evolution:8080/message/sendText/Admin");
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        await client.SendTextAsync(ValidOptions, "123@g.us", "Olá mundo", CancellationToken.None);

        requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("", "key", "inst", "BaseUrl")]
    [InlineData("http://url", "", "inst", "ApiKey")]
    [InlineData("http://url", "key", "", "InstanceId")]
    public async Task Methods_WhenOptionsIncomplete_ShouldThrowInvalidOperationException(
        string baseUrl, string apiKey, string instanceId, string missingParam)
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var options = new WhatsAppChannelOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            InstanceId = instanceId
        };

        var act = () => client.GetConnectionStateAsync(options, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{missingParam}*");
    }
}
