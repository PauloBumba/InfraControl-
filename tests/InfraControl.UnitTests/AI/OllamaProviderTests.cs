using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Infrastructure.AI;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T005/S4 (ADR-037 §10/§13/§14): testes do OllamaProvider
/// — HTTP puro sem SDK, model discovery via GET /api/tags e teste de
/// conexão. Sem Docker: usa HttpClient fake para validar a lógica.
/// </summary>
public sealed class OllamaProviderTests
{

    private static Mock<HttpMessageHandler>? _lastHandlerMock;

    [Fact]
    public void Constructor_deve_configurar_base_url()
    {
        var httpClientFactory = CreateHttpClientFactory();
        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });

        var provider = new OllamaProvider(httpClientFactory, options);

        provider.Name.Should().Be("ollama");
    }

    [Fact]
    public async Task CompleteAsync_sucesso_deve_retornar_resultado_com_tokens()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new
                {
                    response = "resposta gerada",
                    prompt_eval_count = 10,
                    eval_count = 20,
                    done = true
                });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var result = await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt de teste"),
            CancellationToken.None);

        result.Text.Should().Be("resposta gerada");
        result.PromptTokens.Should().Be(10);
        result.CompletionTokens.Should().Be(20);
    }

    [Fact]
    public async Task CompleteAsync_sem_modelo_usa_default_qwen()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new
                {
                    response = "resposta",
                    prompt_eval_count = 5,
                    eval_count = 10,
                    done = true
                });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt"),
            CancellationToken.None);

        var request = GetLastRequest(httpClientFactory);
        var requestBody = await request.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(requestBody);
        json.GetProperty("model").GetString().Should().Be("qwen2.5-coder:1.5b");
    }

    [Fact]
    public async Task CompleteAsync_com_modelo_usa_o_modelo_repassado()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new
                {
                    response = "resposta",
                    prompt_eval_count = 5,
                    eval_count = 10,
                    done = true
                });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "modelo-custom"),
            CancellationToken.None);

        var request = GetLastRequest(httpClientFactory);
        var requestBody = await request.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(requestBody);
        json.GetProperty("model").GetString().Should().Be("modelo-custom");
    }

    [Fact]
    public async Task CompleteAsync_resposta_invalida_lanca_InvalidOperationException()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent("json invalido");
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Resposta inválida do Ollama.");
    }

    [Fact]
    public async Task CompleteAsync_http_error_propaga_excecao()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ListModelsAsync_sucesso_deve_retornar_lista_de_modelos()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new
                {
                    models = new[]
                    {
                        new { name = "qwen2.5-coder:1.5b", digest = "abc123", size = 12345L, modified_at = DateTime.UtcNow },
                        new { name = "llama3:8b", digest = "def456", size = 67890L, modified_at = DateTime.UtcNow }
                    }
                });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().HaveCount(2);
        models[0].Should().Be("qwen2.5-coder:1.5b");
        models[1].Should().Be("llama3:8b");
    }

    [Fact]
    public async Task ListModelsAsync_vazio_retorna_lista_vazia()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new { models = Array.Empty<object>() });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().BeEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_sucesso_retorna_true()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var result = await provider.TestConnectionAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_falha_retorna_false()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var result = await provider.TestConnectionAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_excecao_retorna_false()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                throw new HttpRequestException("conexão recusada");
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        var result = await provider.TestConnectionAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    private static IHttpClientFactory CreateHttpClientFactory(
        Action<HttpRequestMessage, HttpResponseMessage>? configureResponse = null)
    {
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _lastHandlerMock = mockHttpMessageHandler;
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                configureResponse?.Invoke(request, response);
                return response;
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(f => f.CreateClient("ollama"))
            .Returns(httpClient);

        return mockHttpClientFactory.Object;
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static HttpRequestMessage GetLastRequest(IHttpClientFactory factory)
    {
        var invocations = _lastHandlerMock!.Invocations;
        var invocation = invocations.FirstOrDefault(i => i.Method.Name == "SendAsync");
        return invocation?.Arguments[0] as HttpRequestMessage
            ?? throw new InvalidOperationException("Nenhuma requisição foi feita.");
    }

    [Fact]
    public async Task CompleteAsync_com_MaxTokens_manda_num_predict_no_payload_55()
    {
        var httpClientFactory = CreateHttpClientFactory(
            (request, response) =>
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = Json(new { response = "ok", done = true });
            });

        var options = Options.Create(new AiOllamaOptions { BaseUrl = "http://localhost:11434" });
        var provider = new OllamaProvider(httpClientFactory, options);

        await provider.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Copilot,
                "prompt",
                Generation: new AiGenerationOptions(MaxTokens: 64, Temperature: 0.2)),
            CancellationToken.None);

        var request = GetLastRequest(httpClientFactory);
        var body = await request.Content!.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);
        payload.RootElement.GetProperty("options").GetProperty("num_predict").GetInt64()
            .Should().Be(64, "o limite de conclusão §55 deve virar num_predict no Ollama");
    }
}
