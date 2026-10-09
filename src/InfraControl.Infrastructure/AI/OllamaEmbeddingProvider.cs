using System.Net.Http.Json;
using System.Text.Json;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Context;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Adaptador de embeddings do Ollama (FAI-T009/S8 · ADR-038 §2 ·
/// ADR-037 §10): HTTP puro sem SDK — <c>POST /api/embed</c> com
/// <c>{model, input}</c>, devolve <c>embeddings[0]</c>. Modelo de
/// <c>Ai:Ollama:EmbeddingModel</c>; sem modelo configurado lança
/// estado "não configurado" (§44) em vez de inventar vetor.
/// </summary>
public sealed class OllamaEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AiOllamaOptions> options) : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public string Name => "ollama-embedding";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var model = options.Value.EmbeddingModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new AiProviderNotConfiguredException(
                "Modelo de embedding não está configurado (Ai:Ollama:EmbeddingModel — estado não configurado, §44).");
        }

        var httpClient = httpClientFactory.CreateClient("ollama");
        var response = await httpClient.PostAsJsonAsync(
            $"{options.Value.BaseUrl.TrimEnd('/')}/api/embed",
            new { model, input = text },
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<OllamaEmbedResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Resposta inválida do Ollama (embeddings).");

        var embedding = payload.Embeddings?.FirstOrDefault()
            ?? throw new InvalidOperationException("Ollama não devolveu embedding (lista vazia).");

        return embedding;
    }

    private sealed record OllamaEmbedResponse(List<float[]>? Embeddings);
}
