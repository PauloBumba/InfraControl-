using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Infrastructure.AI;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T009/S8 (ADR-038 §2 · ADR-037 §10): embeddings do Ollama via
/// HTTP puro (POST /api/embed) — payload {model, input}, parsing de
/// embeddings[0], estado "não configurado" §44 sem modelo em
/// Ai:Ollama:EmbeddingModel. Sem Docker: HttpClient fake.
/// </summary>
public sealed class OllamaEmbeddingProviderTests
{
    private static (OllamaEmbeddingProvider Provider, List<HttpRequestMessage> Requests) Build(
        AiOllamaOptions options,
        Action<HttpResponseMessage>? configureResponse = null)
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                requests.Add(request);
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                configureResponse?.Invoke(response);
                return response;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(factory => factory.CreateClient("ollama")).Returns(httpClient);

        return (new OllamaEmbeddingProvider(factoryMock.Object, Options.Create(options)), requests);
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task EmbedAsync_devolve_o_primeiro_embedding()
    {
        var (provider, requests) = Build(
            new AiOllamaOptions { BaseUrl = "http://localhost:11434", EmbeddingModel = "nomic-embed-text" },
            response => response.Content = Json(new { embeddings = new[] { new[] { 0.25f, -0.5f } } }));

        var embedding = await provider.EmbedAsync("texto de teste");

        embedding.Should().Equal(0.25f, -0.5f);
        var body = await requests.Single().Content!.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);
        payload.RootElement.GetProperty("model").GetString().Should().Be("nomic-embed-text");
        payload.RootElement.GetProperty("input").GetString().Should().Be("texto de teste");
    }

    [Fact]
    public async Task EmbedAsync_sem_modelo_configurado_e_estado_nao_configurado_44()
    {
        var (provider, _) = Build(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });

        var act = async () => await provider.EmbedAsync("texto");

        var failure = await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
        failure.Which.Message.Should().Contain("não está configurado");
    }

    [Fact]
    public async Task EmbedAsync_resposta_vazia_e_erro_claro()
    {
        var (provider, _) = Build(
            new AiOllamaOptions { BaseUrl = "http://localhost:11434", EmbeddingModel = "nomic-embed-text" },
            response => response.Content = Json(new { embeddings = Array.Empty<float[]>() }));

        var act = async () => await provider.EmbedAsync("texto");

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().Contain("não devolveu embedding");
    }

    [Fact]
    public async Task EmbedAsync_prompt_vazio_e_recusado_ptbr()
    {
        var (provider, _) = Build(new AiOllamaOptions { BaseUrl = "http://localhost:11434", EmbeddingModel = "nomic-embed-text" });

        var act = async () => await provider.EmbedAsync("  ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
