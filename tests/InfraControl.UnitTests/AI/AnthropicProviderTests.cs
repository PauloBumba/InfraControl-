using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// F49-T003: adaptador Anthropic genérico — protocolo próprio
/// (POST /v1/messages + x-api-key + anthropic-version), nunca OpenAI;
/// descoberta via GET /v1/models oficial; D3/D3-A como os demais.
/// </summary>
public sealed class AnthropicProviderTests
{
    private static AiAnthropicOptions CreateOptions(
        string baseUrl = "https://api.anthropic.com",
        string? apiKey = "sk-ant-test",
        string name = "anthropic",
        List<string>? models = null) =>
        new() { Name = name, BaseUrl = baseUrl, ApiKey = apiKey, Models = models ?? [] };

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return factory;
    }

    private sealed class MockHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static StubHttpMessageHandler Responder(
        Func<HttpRequestMessage, HttpResponseMessage> respond) => new(respond);

    private static AsyncStubHttpMessageHandler AsyncResponder(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => new(respond);

    private static HttpResponseMessage Json(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public void Constructor_configura_nome_default_anthropic()
    {
        var provider = new AnthropicProvider(
            Factory(Responder(_ => Json(200, "{}"))),
            Options.Create(CreateOptions(name: "")),
            FakeAiConnectionSource.Null);

        provider.Name.Should().Be("anthropic");
    }

    [Fact]
    public async Task CompleteAsync_envia_protocolo_anthropic_completo()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var handler = AsyncResponder(async request =>
        {
            seen = request;
            body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            return Json(200,
                """{"content":[{"type":"text","text":"resposta claude"}],"usage":{"input_tokens":9,"output_tokens":5}}""");
        });

        var provider = new AnthropicProvider(
            Factory(handler), Options.Create(CreateOptions()), FakeAiConnectionSource.Null);

        var result = await provider.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.IncidentAnalysis, "qual a causa?",
                SystemPrompt: "você é um analista",
                Generation: new AiGenerationOptions(MaxTokens: 256, Temperature: 0.2),
                Model: "claude-sonnet-4-5"),
            CancellationToken.None);

        result.Text.Should().Be("resposta claude");
        result.PromptTokens.Should().Be(9);
        result.CompletionTokens.Should().Be(5);

        seen!.RequestUri!.AbsolutePath.Should().Be("/v1/messages");
        seen.Headers.GetValues("x-api-key").Should().ContainSingle("sk-ant-test");
        seen.Headers.GetValues("anthropic-version").Should().ContainSingle("2023-06-01");
        body.Should().Contain("\"model\":\"claude-sonnet-4-5\"")
            .And.Contain("\"max_tokens\":256")
            .And.Contain("\"temperature\":0.2")
            .And.Contain("\"system\":\"") // system prompt presente (acentos escapados em JSON)
            .And.Contain("analista")
            .And.Contain("qual a causa?");
    }

    [Fact]
    public async Task CompleteAsync_maxTokens_default_quando_nao_informado()
    {
        string? body = null;
        var handler = AsyncResponder(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Json(200, """{"content":[{"type":"text","text":"ok"}]}""");
        });

        var provider = new AnthropicProvider(
            Factory(handler), Options.Create(CreateOptions()), FakeAiConnectionSource.Null);

        await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "oi", Model: "claude-x"),
            CancellationToken.None);

        body.Should().Contain("\"max_tokens\":1024");
    }

    [Fact]
    public async Task CompleteAsync_sem_modelo_lancaNaoConfigurado()
    {
        var provider = new AnthropicProvider(
            Factory(Responder(_ => Json(200, "{}"))),
            Options.Create(CreateOptions()),
            FakeAiConnectionSource.Null);

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "oi", Model: null!),
            CancellationToken.None);

        (await act.Should().ThrowAsync<AiProviderNotConfiguredException>())
            .Which.Message.Should().Contain("Modelo");
    }

    [Fact]
    public async Task CompleteAsync_sem_apikey_lancaNaoConfigurado()
    {
        var provider = new AnthropicProvider(
            Factory(Responder(_ => Json(200, "{}"))),
            Options.Create(CreateOptions(apiKey: null)),
            FakeAiConnectionSource.Null);

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "oi", Model: "claude-x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    [Fact]
    public async Task ListModels_devolve_ids_da_lista_oficial()
    {
        var handler = Responder(_ => Json(200,
            """{"data":[{"id":"claude-sonnet-4-5"},{"id":"claude-haiku-4-5"}]}"""));

        var provider = new AnthropicProvider(
            Factory(handler), Options.Create(CreateOptions()), FakeAiConnectionSource.Null);

        var models = await provider.ListModelsAsync();

        models.Should().Equal("claude-sonnet-4-5", "claude-haiku-4-5");
    }

    [Fact]
    public async Task TestConnection_verifica_v1_models_com_auth()
    {
        Uri? seen = null;
        var handler = Responder(request =>
        {
            seen = request.RequestUri;
            return Json(200, """{"data":[]}""");
        });

        var provider = new AnthropicProvider(
            Factory(handler), Options.Create(CreateOptions()), FakeAiConnectionSource.Null);

        (await provider.TestConnectionAsync()).Should().BeTrue();
        seen!.AbsolutePath.Should().Be("/v1/models");
    }

    [Fact]
    public async Task Cadastro_D3_usa_conexao_do_banco_em_vez_da_config()
    {
        HttpRequestMessage? seen = null;
        var handler = Responder(request =>
        {
            seen = request;
            return Json(200, """{"data":[{"id":"claude-x"}]}""");
        });
        var source = new FakeAiConnectionSource(new AiProviderConnection(
            "meu-anthropic", "https://proxy.local/anthropic", "sk-do-banco",
            [], IsActive: true, Protocol: AiProviderProtocol.Anthropic));

        var provider = new AnthropicProvider(
            Factory(handler),
            Options.Create(CreateOptions(name: "meu-anthropic")),
            source);

        await provider.ListModelsAsync();

        seen!.RequestUri!.ToString().Should().StartWith("https://proxy.local/anthropic/v1/models");
        seen.Headers.GetValues("x-api-key").Should().ContainSingle("sk-do-banco");
    }

    [Fact]
    public async Task Cadastro_inativo_recusa_comSecao44()
    {
        var source = new FakeAiConnectionSource(new AiProviderConnection(
            "anthropic", "https://api.anthropic.com", "sk", [],
            IsActive: false, Protocol: AiProviderProtocol.Anthropic));
        var provider = new AnthropicProvider(
            Factory(Responder(_ => Json(200, "{}"))),
            Options.Create(CreateOptions()),
            source);

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "oi", Model: "claude-x"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<AiProviderNotConfiguredException>())
            .Which.Message.Should().Contain("inativo");
    }
}
