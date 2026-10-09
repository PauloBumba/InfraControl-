using System.Net.Http.Json;
using System.Text.Json;
using InfraControl.Application.AI;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador Ollama em HTTP puro (ADR-037 §10): sem SDK, usando
/// <c>IHttpClientFactory</c> + <c>System.Text.Json</c>. Endpoint interno
/// <c>http://ollama:11434</c> no compose (ADR-041) ou
/// <c>localhost:11434</c> em dev local.
/// </summary>
public sealed class OllamaProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Name => "ollama";

    public OllamaProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOllamaOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("ollama");
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Completa um prompt usando a API de geração do Ollama
    /// (<c>POST /api/generate</c>).
    /// </summary>
    public async Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
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
            $"{_baseUrl}/api/generate",
            ollamaRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(json);

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
    /// </summary>
    public async Task<IReadOnlyList<OllamaModel>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/api/tags",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json);

        return ollamaResponse?.Models ?? Array.Empty<OllamaModel>();
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
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/tags",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // DTOs internos para as respostas do Ollama
    private sealed record OllamaGenerateResponse(
        string? Response,
        long? PromptEvalCount,
        long? EvalCount,
        bool? Done);

    private sealed record OllamaTagsResponse(
        List<OllamaModel>? Models);

    private sealed record OllamaModel(
        string Name,
        string? Digest,
        long? Size,
        DateTime? ModifiedAt);
}
