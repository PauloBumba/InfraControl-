namespace InfraControl.Application.AI;

/// <summary>
/// Opções da seção <c>Ai:Embeddings</c> — provider de embeddings do RAG
/// (ADR-038 §2). <c>Provider</c> = <c>ollama</c> (default, usa
/// <c>Ai:Ollama:EmbeddingModel</c>) ou <c>openai-compatible</c> (OpenAI,
/// OpenRouter, Groq, Together, vLLM, LM Studio, Gemini via endpoint
/// compat). Sem BaseUrl/Model no modo openai-compatible o provider
/// degrada para Ollama — nunca embedding inventado (§44).
/// ApiKey só via env (<c>Ai__Embeddings__ApiKey</c>), nunca no repo.
/// </summary>
public sealed class AiEmbeddingsOptions
{
    /// <summary><c>ollama</c> (default) ou <c>openai-compatible</c>.</summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>Base URL do endpoint OpenAI-compatible (ex.: https://api.openai.com/v1).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Chave de API do provider externo (env apenas).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de embedding (ex.: text-embedding-3-small, gemini-embedding-001).</summary>
    public string? Model { get; set; }
}
