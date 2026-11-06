using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// F49-T003: adaptador Anthropic genérico em HTTP puro (molde
/// ADR-050 §3/§4): <c>POST {BaseUrl}/v1/messages</c> com headers
/// <c>x-api-key</c> + <c>anthropic-version</c> (protocolo próprio — não
/// é OpenAI-compatible). Serve qualquer endpoint que fale o protocolo
/// (api.anthropic.com ou proxy compatível). ADR-057 D3/D3-A: cadastro
/// primeiro; linha inativa recusa (§44); sem linha, a config.
/// </summary>
public sealed class AnthropicProvider : IAiProvider
{
    internal const string AnthropicVersion = "2023-06-01";
    private const int DefaultMaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            // API Anthropic usa snake_case (input_tokens, output_tokens…).
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    private readonly HttpClient _httpClient;
    private readonly AiAnthropicOptions _options;
    private readonly IAiProviderConnectionSource _connectionSource;

    public string Name => string.IsNullOrWhiteSpace(_options.Name)
        ? "anthropic"
        : _options.Name.Trim();

    public AnthropicProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiAnthropicOptions> options,
        IAiProviderConnectionSource connectionSource)
    {
        _httpClient = httpClientFactory.CreateClient("anthropic");
        _options = options.Value;
        _connectionSource = connectionSource;
    }

    /// <summary>
    /// Completa um prompt via <c>/v1/messages</c> (protocolo Anthropic).
    /// Sem BaseUrl/ApiKey o provider fica "não configurado" (§44).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl)
            || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiProviderNotConfiguredException(
                $"Provider Anthropic '{Name}' não está configurado (Ai:Anthropic:BaseUrl/ApiKey).");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new AiProviderNotConfiguredException(
                $"Modelo não especificado para o provider '{Name}'.");
        }

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.Generation?.MaxTokens ?? DefaultMaxTokens,
            ["messages"] = new[]
            {
                new { role = "user", content = request.Prompt }
            },
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["system"] = request.SystemPrompt;
        }

        if (request.Generation?.Temperature is double temperature)
        {
            payload["temperature"] = temperature;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyAuth(httpRequest, apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        AnthropicResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AnthropicResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Resposta inválida do Anthropic.", ex);
        }

        var text = parsed?.Content?
            .Where(block => string.Equals(block.Type, "text", StringComparison.OrdinalIgnoreCase))
            .Select(block => block.Text)
            .Where(blockText => !string.IsNullOrEmpty(blockText))
            .Aggregate(string.Empty, (current, blockText) => current + blockText)
            ?? throw new InvalidOperationException("Resposta inválida do Anthropic.");

        return new AiProviderResult(
            text,
            parsed.Usage?.InputTokens,
            parsed.Usage?.OutputTokens);
    }

    /// <summary>
    /// Descoberta de modelos (F49-T003): <c>GET {BaseUrl}/v1/models</c>
    /// (lista oficial do fornecedor — nunca inventada). Sem conexão,
    /// devolve o cadastro manual da configuração (§44).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl)
            || string.IsNullOrWhiteSpace(apiKey))
        {
            return _options.Models;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, $"{baseUrl}/v1/models");
        ApplyAuth(httpRequest, apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<AnthropicModelsResponse>(json, JsonOptions);

        return parsed?.Data?
            .Select(model => model.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList() ?? [];
    }

    /// <summary>
    /// Teste de conexão (ADR-037 §13): <c>GET /v1/models</c> 2xx.
    /// Sem BaseUrl/ApiKey = false (§44).
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (baseUrl, apiKey) = await ResolveConnectionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(baseUrl)
                || string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get, $"{baseUrl}/v1/models");
            ApplyAuth(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyAuth(HttpRequestMessage request, string apiKey)
    {
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        request.Headers.Accept.ParseAdd("application/json");
    }

    /// <summary>ADR-057 D3/D3-A: cadastro primeiro; inativo recusa; config.</summary>
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

    // DTOs internos (camelCase — JsonSerializerDefaults.Web)
    private sealed class AnthropicResponse
    {
        public List<AnthropicContentBlock>? Content { get; set; }
        public AnthropicUsage? Usage { get; set; }
    }

    private sealed class AnthropicContentBlock
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private sealed class AnthropicUsage
    {
        public long? InputTokens { get; set; }
        public long? OutputTokens { get; set; }
    }

    private sealed class AnthropicModelsResponse
    {
        public List<AnthropicModelInfo>? Data { get; set; }
    }

    private sealed class AnthropicModelInfo
    {
        public string? Id { get; set; }
    }
}
