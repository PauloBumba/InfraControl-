using System.Net.Http.Json;
using System.Text.Json;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador OpenAI-compatible em HTTP puro (ADR-050 §3 · ADR-037 §10):
/// mesmo contrato <c>/chat/completions</c> + <c>/models</c> da OpenAI —
/// cobre OpenAI, OpenRouter, Groq, Together e servidores locais (vLLM,
/// LM Studio, Ollama <c>/v1</c>). Sem SDK de terceiros; a API key é
/// opcional (servidores locais normalmente não exigem). ADR-057
/// (FAI-T011a): BaseUrl/ApiKey vêm do cadastro primeiro (D3); sem
/// linha, a config (D3-A); linha inativa = "não configurado" (§44).
/// </summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    // A API OpenAI usa snake_case no payload/resposta (prompt_tokens,
    // completion_tokens) — SnakeCaseLower + case-insensitive cobre os
    // dois sentidos (mesma escolha do OllamaProvider).
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;
    private readonly AiOpenAiCompatibleOptions _options;
    private readonly IAiProviderConnectionSource _connectionSource;

    public string Name => string.IsNullOrWhiteSpace(_options.Name)
        ? "openai"
        : _options.Name.Trim();

    public OpenAiCompatibleProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOpenAiCompatibleOptions> options,
        IAiProviderConnectionSource connectionSource)
    {
        _httpClient = httpClientFactory.CreateClient("openai-compatible");
        _options = options.Value;
        _connectionSource = connectionSource;
    }

    /// <summary>
    /// Completa um prompt via <c>POST {BaseUrl}/chat/completions</c>
    /// (ADR-050 §3). BaseUrl vazio = estado "não configurado" (§44).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new AiProviderNotConfiguredException(
                $"Provider OpenAI-compatible '{Name}' não está configurado (Ai:OpenAiCompatible:BaseUrl).");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new AiProviderNotConfiguredException(
                $"Modelo não especificado para o provider '{Name}'.");
        }

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }
        messages.Add(new { role = "user", content = request.Prompt });

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages
        };
        if (request.Generation?.Temperature is double temperature)
        {
            payload["temperature"] = temperature;
        }
        if (request.Generation?.MaxTokens is int maxTokens)
        {
            payload["max_tokens"] = maxTokens;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyApiKey(httpRequest, apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        OpenAiChatResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Resposta inválida do provider OpenAI-compatible.", ex);
        }

        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("Resposta inválida do provider OpenAI-compatible.");

        return new AiProviderResult(
            text,
            parsed.Usage?.PromptTokens,
            parsed.Usage?.CompletionTokens);
    }

    /// <summary>
    /// Descoberta de modelos (ADR-037 §14 · ADR-050 §5):
    /// <c>GET {BaseUrl}/models</c>. Sem BaseUrl devolve o cadastro
    /// manual da configuração (provider "não configurado", §44).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return _options.Models;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/models");
        ApplyApiKey(httpRequest, apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<OpenAiModelsResponse>(json, JsonOptions);

        return parsed?.Data?
            .Select(model => model.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList() ?? [];
    }

    /// <summary>
    /// Teste de conexão (ADR-037 §13): <c>GET /models</c> responde 2xx.
    /// Sem BaseUrl = false (não configurado, §44).
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return false;
            }

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/models");
            ApplyApiKey(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ADR-057 D3/D3-A: cadastro primeiro; linha inativa recusa (§44);
    /// sem linha, a configuração.
    /// </summary>
    private async Task<(string BaseUrl, string? ApiKey)> ResolveConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = await _connectionSource.FindConnectionAsync(Name, cancellationToken);
        if (connection is not null)
        {
            if (!connection.IsActive)
            {
                throw new AiProviderNotConfiguredException(
                    $"Provider '{Name}' está inativo no cadastro.");
            }

            return (connection.BaseUrl.TrimEnd('/'), connection.ApiKeyPlain);
        }

        return (_options.BaseUrl?.TrimEnd('/') ?? string.Empty, _options.ApiKey);
    }

    private void ApplyApiKey(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    // DTOs internos (camelCase — JsonSerializerDefaults.Web)
    private sealed class OpenAiChatResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private sealed class OpenAiUsage
    {
        public long? PromptTokens { get; set; }
        public long? CompletionTokens { get; set; }
    }

    private sealed class OpenAiModelsResponse
    {
        public List<OpenAiModel>? Data { get; set; }
    }

    private sealed class OpenAiModel
    {
        public string? Id { get; set; }
    }
}
