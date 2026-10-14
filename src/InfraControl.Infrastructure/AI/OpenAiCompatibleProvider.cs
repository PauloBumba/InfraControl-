using System.Net.Http.Json;
using System.Text.Json;
using InfraControl.Application.AI;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador OpenAI-compatible em HTTP puro (ADR-050 §3 · ADR-037 §10):
/// mesmo contrato <c>/chat/completions</c> + <c>/models</c> da OpenAI —
/// cobre OpenAI, OpenRouter, Groq, Together e servidores locais (vLLM,
/// LM Studio, Ollama <c>/v1</c>). Sem SDK de terceiros; a API key é
/// opcional (servidores locais normalmente não exigem).
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

    public string Name => string.IsNullOrWhiteSpace(_options.Name)
        ? "openai"
        : _options.Name.Trim();

    public OpenAiCompatibleProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOpenAiCompatibleOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("openai-compatible");
        _options = options.Value;
    }

    /// <summary>
    /// Completa um prompt via <c>POST {BaseUrl}/chat/completions</c>
    /// (ADR-050 §3). BaseUrl vazio = estado "não configurado" (§44).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
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
            $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyApiKey(httpRequest);

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
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return _options.Models;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl.TrimEnd('/')}/models");
        ApplyApiKey(httpRequest);

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
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return false;
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_options.BaseUrl.TrimEnd('/')}/models");
            ApplyApiKey(httpRequest);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyApiKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
