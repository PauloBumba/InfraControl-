namespace InfraControl.Application.AI;

/// <summary>
/// Opções de configuração da seção <c>Ai:Ollama</c> (ADR-037 §2).
/// </summary>
public sealed record AiOllamaOptions(
    string BaseUrl);

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
