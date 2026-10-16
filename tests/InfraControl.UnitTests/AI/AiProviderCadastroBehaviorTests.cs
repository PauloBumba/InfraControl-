using System.Net;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// ADR-057 (FAI-T011a): comportamento dos adaptadores com cadastro
/// — BaseUrl/ApiKey do cadastro vencem a config (D3); linha inativa =
/// "não configurado" (§44); sem linha = config (D3-A).
/// </summary>
public sealed class AiProviderCadastroBehaviorTests
{
    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static IHttpClientFactory Factory(HttpMessageHandler handler) =>
        new StubHttpClientFactory(handler);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task Ollama_Usa_BaseUrl_Do_Cadastro()
    {
        var handler = new CaptureHandler(_ => JsonResponse(new
        {
            response = "ok",
            done = true
        }));
        var connection = new AiProviderConnection(
            "ollama", "http://cadastro:9999", ApiKeyPlain: null, Models: [], IsActive: true);
        var provider = new OllamaProvider(
            Factory(handler),
            Options.Create(new AiOllamaOptions { BaseUrl = "http://config:11434" }),
            new FakeAiConnectionSource(connection));

        var result = await provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "llama3.2"));

        result.Text.Should().Be("ok");
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .StartWith("http://cadastro:9999/api/generate");
    }

    [Fact]
    public async Task Ollama_Sem_Linha_No_Cadastro_Cai_Na_Config()
    {
        var handler = new CaptureHandler(_ => JsonResponse(new
        {
            response = "ok",
            done = true
        }));
        var provider = new OllamaProvider(
            Factory(handler),
            Options.Create(new AiOllamaOptions { BaseUrl = "http://config:11434" }),
            FakeAiConnectionSource.Null);

        await provider.CompleteAsync(new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "llama3.2"));

        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .StartWith("http://config:11434/api/generate");
    }

    [Fact]
    public async Task Ollama_Inativo_No_Cadastro_Recusa()
    {
        var handler = new CaptureHandler(_ => JsonResponse(new { response = "ok", done = true }));
        var connection = new AiProviderConnection(
            "ollama", "http://cadastro:9999", null, [], IsActive: false);
        var provider = new OllamaProvider(
            Factory(handler),
            Options.Create(new AiOllamaOptions { BaseUrl = "http://config:11434" }),
            new FakeAiConnectionSource(connection));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "llama3.2"));

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*inativo*");
        handler.LastRequest.Should().BeNull("nenhuma chamada HTTP deve sair");
    }

    [Fact]
    public async Task OpenAi_Usa_Bearer_Do_Cadastro_Descifrado()
    {
        var handler = new CaptureHandler(_ => JsonResponse(new
        {
            choices = new[] { new { message = new { content = "ok" } } }
        }));
        var connection = new AiProviderConnection(
            "openai", "https://api.cadastro.example", "sk-cadastro", [], IsActive: true);
        var provider = new OpenAiCompatibleProvider(
            Factory(handler),
            Options.Create(new AiOpenAiCompatibleOptions
            {
                BaseUrl = "https://api.config.example",
                ApiKey = "sk-config"
            }),
            new FakeAiConnectionSource(connection));

        await provider.CompleteAsync(new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gpt-4o-mini"));

        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .StartWith("https://api.cadastro.example/chat/completions");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sk-cadastro");
    }

    [Fact]
    public async Task Gemini_Inativo_No_Cadastro_Recusa()
    {
        var handler = new CaptureHandler(_ => JsonResponse(new { }));
        var connection = new AiProviderConnection(
            "gemini", "https://aistudio.example", "key", [], IsActive: false);
        var provider = new GeminiProvider(
            Factory(handler),
            Options.Create(new AiGeminiOptions
            {
                BaseUrl = "https://config.example",
                ApiKey = "key-config"
            }),
            new FakeAiConnectionSource(connection));

        var act = () => provider.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt", Model: "gemini-flash"));

        await act.Should().ThrowAsync<AiProviderNotConfiguredException>()
            .WithMessage("*inativo*");
    }
}
