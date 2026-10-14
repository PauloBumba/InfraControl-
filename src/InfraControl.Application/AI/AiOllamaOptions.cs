namespace InfraControl.Application.AI;

/// <summary>
/// Opções de configuração da seção <c>Ai:Ollama</c> (ADR-037 §2).
/// <c>EmbeddingModel</c> (S8 · ADR-038): modelo de embedding local —
/// configurado em <c>Ai:Ollama:EmbeddingModel</c> (ex.:
/// <c>nomic-embed-text</c>); nulo = embeddings não configurados (§44).
/// </summary>
public sealed class AiOllamaOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? EmbeddingModel { get; set; }
}

/// <summary>
/// Opções de configuração da seção <c>Ai:Routing</c> (ADR-037 §15).
/// </summary>
public sealed class AiRoutingOptionsConfiguration
{
    public AiRoutingModelOptions Copilot { get; set; } = new();
    public AiRoutingModelOptions IncidentAnalysis { get; set; } = new();
    public AiRoutingModelOptions KnowledgeGeneration { get; set; } = new();
    public AiRoutingModelOptions Reporting { get; set; } = new();
    public AiRoutingModelOptions Automation { get; set; } = new();
}

/// <summary>
/// Opções de modelo por capacidade (default + fallback). Desde o ADR-050
/// cada capacidade também escolhe o <c>Provider</c> (vazio = "ollama",
/// backward compat com o ADR-037 §15) e o <c>FallbackProvider</c>
/// (vazio = o mesmo provider primário).
/// </summary>
public sealed class AiRoutingModelOptions
{
    public string? Provider { get; set; }
    public string DefaultModel { get; set; } = string.Empty;
    public string? FallbackProvider { get; set; }
    public string? FallbackModel { get; set; }
}
