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
/// Opções de configuração da seção <c>Ai:Routing</c> (ADR-037 §15) com o
/// modelo único <c>Default</c> no topo (ADR-065): as entradas por
/// capacidade são override opcional — campo não declarado herda o
/// <c>Default</c>. Config legada só com as 5 entradas continua válida
/// (<c>Default</c> fica vazio e a cascatada não muda o resultado).
/// </summary>
public sealed class AiRoutingOptionsConfiguration
{
    public AiRoutingModelOptions Default { get; set; } = new();
    public AiRoutingModelOptions Copilot { get; set; } = new();
    public AiRoutingModelOptions IncidentAnalysis { get; set; } = new();
    public AiRoutingModelOptions KnowledgeGeneration { get; set; } = new();
    public AiRoutingModelOptions Reporting { get; set; } = new();
    public AiRoutingModelOptions Automation { get; set; } = new();

    /// <summary>
    /// ADR-065 §4: efetiva da capacidade = override por capacidade mesclado
    /// campo a campo com o modelo único (<c>Default</c>). Capacidade fora
    /// do enum não herda nada (<c>null</c> = "não está configurada").
    /// </summary>
    public AiRoutingModelOptions? EffectiveFor(AiCapability capability)
    {
        var specific = capability switch
        {
            AiCapability.Copilot => Copilot,
            AiCapability.IncidentAnalysis => IncidentAnalysis,
            AiCapability.KnowledgeGeneration => KnowledgeGeneration,
            AiCapability.Reporting => Reporting,
            AiCapability.Automation => Automation,
            _ => null
        };

        return specific?.MergeWith(Default);
    }
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

    /// <summary>Configuração efetiva declarada (override) ou vazia.</summary>
    public bool IsDeclared => !string.IsNullOrWhiteSpace(DefaultModel);

    /// <summary>
    /// ADR-065: merge campo a campo com o modelo único — o que está
    /// declarado no override vence; o resto é herdado do <c>Default</c>.
    /// </summary>
    public AiRoutingModelOptions MergeWith(AiRoutingModelOptions @default)
        => new()
        {
            Provider = FirstDeclared(Provider, @default.Provider),
            DefaultModel = IsDeclared ? DefaultModel : @default.DefaultModel,
            FallbackProvider = FallbackProvider ?? @default.FallbackProvider,
            FallbackModel = FallbackModel ?? @default.FallbackModel
        };

    private static string? FirstDeclared(string? candidate, string? fallback)
        => !string.IsNullOrWhiteSpace(candidate) ? candidate : fallback;
}
