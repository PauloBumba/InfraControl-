using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.AI;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador Google Gemini em HTTP puro (ADR-050 §4 · ADR-037 §10):
/// <c>POST {BaseUrl}/models/{model}:generateContent</c> com a API key
/// como query string (a API do Google a exige). Sem SDK de terceiros.
/// </summary>
public sealed class GeminiProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    private readonly HttpClient _httpClient;
    private readonly AiGeminiOptions _options;

    public string Name => string.IsNullOrWhiteSpace(_options.Name)
        ? "gemini"
        : _options.Name.Trim();

    public GeminiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiGeminiOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("gemini");
        _options = options.Value;
    }

    /// <summary>
    /// Completa um prompt via <c>:generateContent</c> (ADR-050 §4).
    /// Sem BaseUrl/ApiKey o provider fica "não configurado" (§44).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiProviderNotConfiguredException(
                $"Provider Gemini '{Name}' não está configurado (Ai:Gemini:BaseUrl/ApiKey).");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new AiProviderNotConfiguredException(
                $"Modelo não especificado para o provider '{Name}'.");
        }

        var payload = new Dictionary<string, object>
        {
            ["contents"] = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = request.Prompt } }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["systemInstruction"] = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            };
        }

        if (request.Generation?.Temperature is double temperature
            || request.Generation?.MaxTokens is int)
        {
            var generationConfig = new Dictionary<string, object>();
            if (request.Generation?.Temperature is double t)
            {
                generationConfig["temperature"] = t;
            }
            if (request.Generation?.MaxTokens is int maxTokens)
            {
                generationConfig["maxOutputTokens"] = maxTokens;
            }
            payload["generationConfig"] = generationConfig;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/"
            + $"{Uri.EscapeDataString(request.Model)}:generateContent"
            + $"?key={Uri.EscapeDataString(_options.ApiKey)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        GeminiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Resposta inválida do Gemini.", ex);
        }

        var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .Select(part => part.Text)
            .Where(textPart => !string.IsNullOrEmpty(textPart))
            .Aggregate(string.Empty, (current, part) => current + part)
            ?? throw new InvalidOperationException("Resposta inválida do Gemini.");

        return new AiProviderResult(
            text,
            parsed.UsageMetadata?.PromptTokenCount,
            parsed.UsageMetadata?.CandidatesTokenCount);
    }

    /// <summary>
    /// Descoberta de modelos (ADR-037 §14 · ADR-050 §5):
    /// <c>GET {BaseUrl}/models</c> (nomes no formato <c>models/gemini-…</c>
    /// — o prefixo é removido). Sem BaseUrl/ApiKey devolve o cadastro
    /// manual da configuração (§44).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.Models;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/models"
            + $"?key={Uri.EscapeDataString(_options.ApiKey)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<GeminiModelsResponse>(json, JsonOptions);

        return parsed?.Models?
            .Select(model => model.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.StartsWith("models/", StringComparison.Ordinal)
                ? name["models/".Length..]
                : name)
            .ToList() ?? [];
    }

    /// <summary>
    /// Teste de conexão (ADR-037 §13): <c>GET /models</c> responde 2xx.
    /// Sem BaseUrl/ApiKey = false (não configurado, §44).
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return false;
        }

        try
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/models"
                + $"?key={Uri.EscapeDataString(_options.ApiKey)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // DTOs internos (camelCase — JsonSerializerDefaults.Web)
    private sealed class GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
        public GeminiUsage? UsageMetadata { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        public string? Text { get; set; }
    }

    private sealed class GeminiUsage
    {
        public long? PromptTokenCount { get; set; }
        public long? CandidatesTokenCount { get; set; }
    }

    private sealed class GeminiModelsResponse
    {
        public List<GeminiModelInfo>? Models { get; set; }
    }

    private sealed class GeminiModelInfo
    {
        public string? Name { get; set; }
    }
}
