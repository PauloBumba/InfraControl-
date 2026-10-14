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
/// ADR-050 §4: testes do GeminiProvider — HTTP puro sem SDK, API key
/// obrigatória em query string, :generateContent e /models. Sem
/// Docker/chave: usa HttpClient fake para validar a lógica.
/// </summary>
public sealed class GeminiProviderTests
{
    private static Mock<HttpMessageHandler>? _lastHandlerMock;

    private static AiGeminiOptions CreateOptions(
        string baseUrl = "https://generativelanguage.googleapis.com/v1beta",
        string? apiKey = "key-test",
        string name = "gemini",
        List<string>? models = null) =>
        new() { Name = name, BaseUrl = baseUrl, ApiKey = apiKey, Models = models ?? [] };

    [Fact]
    public void Constructor_configura_nome_default_gemini()
    {
        var provider = new GeminiProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(name: "")));

        provider.Name.Should().Be("gemini");
    }

    [Fact]
    public async Task CompleteAsync_sucesso_retorna_texto_com_tokens()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = "resposta gemini" } } } }
                },
                usageMetadata = new { promptTokenCount = 7, candidatesTokenCount = 14 }
            });
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        var result = await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "prompt", Model: "gemini-2.0-flash"),
            CancellationToken.None);

        result.Text.Should().Be("resposta gemini");
        result.PromptTokens.Should().Be(7);
        result.CompletionTokens.Should().Be(14);
    }

    [Fact]
    public async Task CompleteAsync_envia_endpoint_generateContent_com_modelo_e_key()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = "ok" } } } } }
            });
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "prompt", Model: "gemini-2.0-flash"),
            CancellationToken.None);

        var uri = GetLastRequest(factory).RequestUri!;
        uri.AbsolutePath.Should().Be("/v1beta/models/gemini-2.0-flash:generateContent");
        uri.Query.Should().Contain("key=key-test");
    }

    [Fact]
    public async Task CompleteAsync_monta_system_instruction()
    {
        string? capturedBody = null;
        var factory = CreateHttpClientFactory((request, response) =>
        {
            // captura ANTES do dispose do HttpRequestMessage no provider
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            response.Content = Json(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = "ok" } } } } }
            });
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        await provider.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.IncidentAnalysis,
                "pergunta",
                SystemPrompt: "Você é o investigador.",
                Model: "gemini-2.0-flash"),
            CancellationToken.None);

        using var payload = JsonDocument.Parse(capturedBody!);
        payload.RootElement.GetProperty("systemInstruction")
            .GetProperty("parts")[0].GetProperty("text").GetString()
            .Should().Be("Você é o investigador.");
    }

    [Fact]
    public async Task CompleteAsync_mapeia_generationConfig()
    {
        string? capturedBody = null;
        var factory = CreateHttpClientFactory((request, response) =>
        {
            // captura ANTES do dispose do HttpRequestMessage no provider
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            response.Content = Json(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = "ok" } } } } }
            });
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        await provider.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.IncidentAnalysis,
                "prompt",
                Generation: new AiGenerationOptions(MaxTokens: 128, Temperature: 0.4),
                Model: "gemini-2.0-flash"),
            CancellationToken.None);

        using var payload = JsonDocument.Parse(capturedBody!);
        var config = payload.RootElement.GetProperty("generationConfig");
        config.GetProperty("maxOutputTokens").GetInt64().Should().Be(128);
        config.GetProperty("temperature").GetDouble().Should().Be(0.4);
    }

    [Fact]
    public async Task CompleteAsync_sem_base_url_lanca_AiProviderNotConfiguredException()
    {
        var provider = new GeminiProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(baseUrl: "")));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "prompt", Model: "gemini-2.0-flash"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*Ai:Gemini:BaseUrl/ApiKey*");
    }

    [Fact]
    public async Task CompleteAsync_sem_api_key_lanca_AiProviderNotConfiguredException()
    {
        var provider = new GeminiProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(apiKey: "")));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "prompt", Model: "gemini-2.0-flash"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*Ai:Gemini:BaseUrl/ApiKey*");
    }

    [Fact]
    public async Task ListModelsAsync_descobre_modelos_e_remove_prefixo_models()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new
            {
                models = new[]
                {
                    new { name = "models/gemini-2.0-flash" },
                    new { name = "models/gemini-1.5-pro" }
                }
            });
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().Equal("gemini-2.0-flash", "gemini-1.5-pro");
    }

    [Fact]
    public async Task ListModelsAsync_sem_configuracao_devolve_cadastro_manual()
    {
        var provider = new GeminiProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(apiKey: "", models: ["gemini-custom"])));

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().Equal("gemini-custom");
    }

    [Fact]
    public async Task TestConnectionAsync_sucesso_retorna_true()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.OK;
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        (await provider.TestConnectionAsync(CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_sem_api_key_retorna_false()
    {
        var provider = new GeminiProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(apiKey: "")));

        (await provider.TestConnectionAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_falha_http_retorna_false()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.BadRequest;
        });

        var provider = new GeminiProvider(factory, Options.Create(CreateOptions()));

        (await provider.TestConnectionAsync(CancellationToken.None)).Should().BeFalse();
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
            .Setup(f => f.CreateClient("gemini"))
            .Returns(httpClient);

        return mockHttpClientFactory.Object;
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static HttpRequestMessage GetLastRequest(IHttpClientFactory factory)
    {
        var invocation = _lastHandlerMock!.Invocations
            .FirstOrDefault(i => i.Method.Name == "SendAsync");
        return invocation?.Arguments[0] as HttpRequestMessage
            ?? throw new InvalidOperationException("Nenhuma requisição foi feita.");
    }
}
