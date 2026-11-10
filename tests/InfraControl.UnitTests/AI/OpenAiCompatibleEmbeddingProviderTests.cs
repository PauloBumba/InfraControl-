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
/// ADR-038 §2: embeddings OpenAI-compatible (POST {BaseUrl}/embeddings,
/// {model, input} → data[0].embedding) — cobre OpenAI, OpenRouter, Groq,
/// vLLM, LM Studio. Bearer só quando ApiKey configurada; BaseUrl/Model
/// ausentes = estado "não configurado" §44. Sem Docker: HttpClient fake.
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProviderTests
{
    private sealed record CapturedRequest(
        string? Url,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Body);

    private static (OpenAiCompatibleEmbeddingProvider Provider, List<CapturedRequest> Requests) Build(
        AiEmbeddingsOptions options,
        Action<HttpResponseMessage>? configureResponse = null)
    {
        var requests = new List<CapturedRequest>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken ct) =>
                CaptureAsync(request, ct, requests, configureResponse));

        var httpClient = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(factory => factory.CreateClient("openai-compatible-embeddings")).Returns(httpClient);

        return (new OpenAiCompatibleEmbeddingProvider(factoryMock.Object, Options.Create(options)), requests);
    }

    private static async Task<HttpResponseMessage> CaptureAsync(
        HttpRequestMessage request,
        CancellationToken ct,
        List<CapturedRequest> requests,
        Action<HttpResponseMessage>? configureResponse)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(ct);
        requests.Add(new CapturedRequest(
            request.RequestUri?.ToString(),
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            body));
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        configureResponse?.Invoke(response);
        return response;
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task EmbedAsync_devolve_o_embedding_e_envia_model_input_e_bearer()
    {
        var (provider, requests) = Build(
            new AiEmbeddingsOptions
            {
                BaseUrl = "https://api.openai.com/v1",
                Model = "text-embedding-3-small",
                ApiKey = "sk-test",
            },
            response => response.Content = Json(new
            {
                data = new[] { new { embedding = new[] { 0.1f, -0.2f } } }
            }));

        var embedding = await provider.EmbedAsync("texto de teste");

        embedding.Should().Equal(0.1f, -0.2f);
        var request = requests.Single();
        request.Url.Should().Be("https://api.openai.com/v1/embeddings");
        request.AuthorizationScheme.Should().Be("Bearer");
        request.AuthorizationParameter.Should().Be("sk-test");
        using var payload = JsonDocument.Parse(request.Body!);
        payload.RootElement.GetProperty("model").GetString().Should().Be("text-embedding-3-small");
        payload.RootElement.GetProperty("input").GetString().Should().Be("texto de teste");
    }

    [Fact]
    public async Task EmbedAsync_sem_api_key_nao_envia_authorization()
    {
        var (provider, requests) = Build(
            new AiEmbeddingsOptions { BaseUrl = "http://localhost:1234/v1", Model = "embed" },
            response => response.Content = Json(new { data = new[] { new { embedding = new[] { 1f } } } }));

        await provider.EmbedAsync("texto");

        requests.Single().AuthorizationScheme.Should().BeNull();
    }

    [Fact]
    public async Task EmbedAsync_sem_base_url_ou_modelo_e_estado_nao_configurado_44()
    {
        var (provider, _) = Build(new AiEmbeddingsOptions { Provider = "openai-compatible" });

        var act = async () => await provider.EmbedAsync("texto");

        var failure = await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
        failure.Which.Message.Should().Contain("não configurados");
    }

    [Fact]
    public async Task EmbedAsync_resposta_sem_vetor_e_erro_claro()
    {
        var (provider, _) = Build(
            new AiEmbeddingsOptions { BaseUrl = "http://x/v1", Model = "embed" },
            response => response.Content = Json(new { data = Array.Empty<object>() }));

        var act = async () => await provider.EmbedAsync("texto");

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().Contain("não devolveu vetor");
    }

    [Fact]
    public async Task EmbedAsync_prompt_vazio_e_recusado_ptbr()
    {
        var (provider, _) = Build(new AiEmbeddingsOptions { BaseUrl = "http://x/v1", Model = "embed" });

        var act = async () => await provider.EmbedAsync("  ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
