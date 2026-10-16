using System.Net.Http.Json;
using System.Text.Json;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador Ollama em HTTP puro (ADR-037 §10): sem SDK, usando
/// <c>IHttpClientFactory</c> + <c>System.Text.Json</c>. Endpoint interno
/// <c>http://ollama:11434</c> no compose (ADR-041) ou
/// <c>localhost:11434</c> em dev local. ADR-057 (FAI-T011a): BaseUrl
/// vem do cadastro primeiro (D3); sem linha, a config (D3-A); linha
/// inativa = "não configurado" (§44).
/// </summary>
public sealed class OllamaProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _configBaseUrl;
    private readonly IAiProviderConnectionSource _connectionSource;

    public string Name => "ollama";

    public OllamaProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOllamaOptions> options,
        IAiProviderConnectionSource connectionSource)
    {
        _httpClient = httpClientFactory.CreateClient("ollama");
        _configBaseUrl = options.Value.BaseUrl.TrimEnd('/');
        _connectionSource = connectionSource;
    }

    /// <summary>
    /// Completa um prompt usando a API de geração do Ollama
    /// (<c>POST /api/generate</c>).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = await ResolveBaseUrlAsync(cancellationToken);

        var ollamaRequest = new
        {
            model = request.Model ?? "qwen2.5-coder:1.5b",
            prompt = request.Prompt,
            system = request.SystemPrompt,
            stream = false,
            options = new
            {
                num_predict = request.Generation?.MaxTokens,
                temperature = request.Generation?.Temperature
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/api/generate",
            ollamaRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        OllamaGenerateResponse? ollamaResponse;
        try
        {
            ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Resposta inválida do Ollama.", ex);
        }

        if (ollamaResponse == null)
        {
            throw new InvalidOperationException("Resposta inválida do Ollama.");
        }

        return new AiProviderResult(
            Text: ollamaResponse.Response ?? string.Empty,
            PromptTokens: ollamaResponse.PromptEvalCount,
            CompletionTokens: ollamaResponse.EvalCount);
    }

    /// <summary>
    /// Descobre os modelos instalados no Ollama (ADR-037 §14):
    /// <c>GET /api/tags</c>. Serve também como teste de conexão (§13).
    /// Contrato do <see cref="IAiProvider"/>: devolve os nomes.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var baseUrl = await ResolveBaseUrlAsync(cancellationToken);

        var response = await _httpClient.GetAsync(
            $"{baseUrl}/api/tags",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, JsonOptions);

        return ollamaResponse?.Models?
            .Select(model => model.Name)
            .ToList() ?? new List<string>();
    }

    /// <summary>
    /// Testa a conexão com o Ollama (ADR-037 §13): chama
    /// <c>GET /api/tags</c> e verifica se retorna 200.
    /// </summary>
    public async Task<bool> TestConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = await ResolveBaseUrlAsync(cancellationToken);
            var response = await _httpClient.GetAsync(
                $"{baseUrl}/api/tags",
                cancellationToken);
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
    private async Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionSource.FindConnectionAsync(Name, cancellationToken);
        if (connection is not null)
        {
            if (!connection.IsActive)
            {
                throw new AiProviderNotConfiguredException(
                    $"Provider '{Name}' está inativo no cadastro.");
            }

            return connection.BaseUrl.TrimEnd('/');
        }

        return _configBaseUrl;
    }

    // DTOs internos para as respostas do Ollama
    private sealed record OllamaGenerateResponse(
        string? Response,
        long? PromptEvalCount,
        long? EvalCount,
        bool? Done);

    private sealed record OllamaTagsResponse(
        List<OllamaModel>? Models);

    public sealed record OllamaModel(
        string Name,
        string? Digest,
        long? Size,
        DateTime? ModifiedAt);
}
