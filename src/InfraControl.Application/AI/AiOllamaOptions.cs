namespace InfraControl.Application.AI;

/// <summary>
/// Opções de configuração da seção <c>Ai:Ollama</c> (ADR-037 §2).
/// <c>EmbeddingModel</c> (S8 · ADR-038): modelo de embedding local —
/// configurado em <c>Ai:Ollama:EmbeddingModel</c> (ex.:
/// <c>nomic-embed-text</c>); nulo = embeddings não configurados (§44).
/// </summary>
public sealed record AiOllamaOptions(
    string BaseUrl,
    string? EmbeddingModel = null);

/// <summary>
/// Opções de configuração da seção <c>Ai:Routing</c> (ADR-037 §15).
/// </summary>
public sealed record AiRoutingOptionsConfiguration(
    AiRoutingModelOptions Copilot,
    AiRoutingModelOptions IncidentAnalysis,
    AiRoutingModelOptions KnowledgeGeneration,
    AiRoutingModelOptions Reporting,
    AiRoutingModelOptions Automation);

/// <summary>
/// Opções de modelo por capacidade (default + fallback).
/// </summary>
public sealed record AiRoutingModelOptions(
    string DefaultModel,
    string? FallbackModel = null);
