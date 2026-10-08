namespace InfraControl.Application.AI;

/// <summary>
/// Capacidades de IA por caso de uso (ADR-037 · docs/34 §15): o router
/// mapeia cada capacidade para um (provider, modelo) — execuções são
/// registradas por capacidade para telemetria/custo (§43/§55).
/// </summary>
public enum AiCapability
{
    /// <summary>Assistente de operação (Fase 1 — AI Copilot, §22).</summary>
    Copilot = 0,

    /// <summary>Análise de incidentes/alertas (Fase 2 — Investigation, §26).</summary>
    IncidentAnalysis = 1,

    /// <summary>Geração/enriquecimento de conteúdo de Knowledge (§31).</summary>
    KnowledgeGeneration = 2,

    /// <summary>Relatórios e interpretação de indicadores (Fase 4, §37).</summary>
    Reporting = 3,

    /// <summary>Decisão/ação em automações — LEVEL ≥1, exige ADR (Fase 5, §38–§40).</summary>
    Automation = 4,
}
