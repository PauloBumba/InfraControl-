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
/// ADR-050 §3: testes do OpenAiCompatibleProvider — HTTP puro sem SDK,
/// Bearer opcional, /chat/completions e /models. Sem Docker/chave:
/// usa HttpClient fake para validar a lógica.
/// </summary>
public sealed class OpenAiCompatibleProviderTests
{
    private static Mock<HttpMessageHandler>? _lastHandlerMock;

    private static AiOpenAiCompatibleOptions CreateOptions(
        string baseUrl = "https://api.openai.com/v1",
        string? apiKey = "sk-test",
        string name = "openai",
        List<string>? models = null) =>
        new() { Name = name, BaseUrl = baseUrl, ApiKey = apiKey, Models = models ?? [] };

    [Fact]
    public void Constructor_configura_nome_default_openai()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(name: "")));

        provider.Name.Should().Be("openai");
    }

    [Fact]
    public void Constructor_configura_nome_customizado()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(name: "groq")));

        provider.Name.Should().Be("groq");
    }

    [Fact]
    public async Task CompleteAsync_sucesso_retorna_texto_com_tokens()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.OK;
            response.Content = Json(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "resposta openai" } } },
                usage = new { prompt_tokens = 11, completion_tokens = 22 }
            });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        var result = await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt de teste", Model: "gpt-4o-mini"),
            CancellationToken.None);

        result.Text.Should().Be("resposta openai");
        result.PromptTokens.Should().Be(11);
        result.CompletionTokens.Should().Be(22);
    }

    [Fact]
    public async Task CompleteAsync_envia_endpoint_chat_completions()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new { choices = new[] { new { message = new { content = "ok" } } } });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"),
            CancellationToken.None);

        GetLastRequest(factory).RequestUri!.AbsolutePath.Should().Be("/v1/chat/completions");
    }

    [Fact]
    public async Task CompleteAsync_envia_bearer_quando_ha_api_key()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new { choices = new[] { new { message = new { content = "ok" } } } });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions(apiKey: "sk-secreta")));

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"),
            CancellationToken.None);

        GetLastRequest(factory).Headers.Authorization.Should().NotBeNull();
        GetLastRequest(factory).Headers.Authorization!.Scheme.Should().Be("Bearer");
        GetLastRequest(factory).Headers.Authorization!.Parameter.Should().Be("sk-secreta");
    }

    [Fact]
    public async Task CompleteAsync_sem_api_key_nao_envia_authorization()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new { choices = new[] { new { message = new { content = "ok" } } } });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions(apiKey: null)));

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"),
            CancellationToken.None);

        GetLastRequest(factory).Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_monta_system_e_user_messages()
    {
        string? capturedBody = null;
        var factory = CreateHttpClientFactory((request, response) =>
        {
            // captura ANTES do dispose do HttpRequestMessage no provider
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            response.Content = Json(new { choices = new[] { new { message = new { content = "ok" } } } });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        await provider.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Copilot,
                "pergunta",
                SystemPrompt: "Você é o copilot.",
                Model: "gpt-4o-mini"),
            CancellationToken.None);

        using var payload = JsonDocument.Parse(capturedBody!);
        var messages = payload.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[1].GetProperty("role").GetString().Should().Be("user");
    }

    [Fact]
    public async Task CompleteAsync_sem_base_url_lanca_AiProviderNotConfiguredException()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(baseUrl: "")));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*Ai:OpenAiCompatible:BaseUrl*");
    }

    [Fact]
    public async Task CompleteAsync_sem_modelo_lanca_AiProviderNotConfiguredException()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions()));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*Modelo não especificado*");
    }

    [Fact]
    public async Task CompleteAsync_http_error_propaga_excecao()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.InternalServerError;
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ListModelsAsync_descobre_modelos_via_endpoint()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.Content = Json(new
            {
                data = new[]
                {
                    new { id = "gpt-4o-mini" },
                    new { id = "gpt-4o" }
                }
            });
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().Equal("gpt-4o-mini", "gpt-4o");
    }

    [Fact]
    public async Task ListModelsAsync_sem_base_url_devolve_cadastro_manual()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(baseUrl: "", models: ["modelo-a", "modelo-b"])));

        var models = await provider.ListModelsAsync(CancellationToken.None);

        models.Should().Equal("modelo-a", "modelo-b");
    }

    [Fact]
    public async Task TestConnectionAsync_sucesso_retorna_true()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.OK;
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

        (await provider.TestConnectionAsync(CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_sem_base_url_retorna_false()
    {
        var provider = new OpenAiCompatibleProvider(
            CreateHttpClientFactory(),
            Options.Create(CreateOptions(baseUrl: "")));

        (await provider.TestConnectionAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_falha_http_retorna_false()
    {
        var factory = CreateHttpClientFactory((request, response) =>
        {
            response.StatusCode = HttpStatusCode.Unauthorized;
        });

        var provider = new OpenAiCompatibleProvider(factory, Options.Create(CreateOptions()));

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
            .Setup(f => f.CreateClient("openai-compatible"))
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
