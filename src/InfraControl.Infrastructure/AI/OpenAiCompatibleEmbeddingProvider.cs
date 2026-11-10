using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Context;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Provider de embeddings OpenAI-compatible (ADR-038 §2):
/// <c>POST {BaseUrl}/embeddings</c> com <c>{model, input}</c>, devolve
/// <c>data[0].embedding</c>. Cobre OpenAI, OpenRouter, Groq, Together,
/// vLLM, LM Studio e Gemini via endpoint compat — a chave vai em
/// <c>Authorization: Bearer</c> (env <c>Ai__Embeddings__ApiKey</c>).
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AiEmbeddingsOptions> options) : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "openai-compatible-embedding";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl) || string.IsNullOrWhiteSpace(opts.Model))
        {
            throw new AiProviderNotConfiguredException(
                "Embeddings openai-compatible não configurados (Ai:Embeddings:BaseUrl/Model — estado não configurado, §44).");
        }

        var httpClient = httpClientFactory.CreateClient("openai-compatible-embeddings");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{opts.BaseUrl.TrimEnd('/')}/embeddings")
        {
            Content = JsonContent.Create(new { model = opts.Model, input = text }, options: JsonOptions),
        };
        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Resposta inválida do provider de embeddings.");

        var embedding = payload.Data?.FirstOrDefault()?.Embedding
            ?? throw new InvalidOperationException("Provider de embeddings não devolveu vetor.");

        return embedding;
    }

    private sealed record EmbedResponse(List<EmbedData>? Data);

    private sealed record EmbedData(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
