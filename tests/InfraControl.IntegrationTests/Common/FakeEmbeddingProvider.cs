using InfraControl.Application.AI.Context;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Embedding falso p/ o host de teste — a ingesta de Knowledge degrada
/// sem <c>IVectorStore</c> registrado (ADR-038 §46) e nunca chama Embed.
/// </summary>
public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public static readonly FakeEmbeddingProvider Instance = new();

    public string Name => "fake-embedding";

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<float>());
}
