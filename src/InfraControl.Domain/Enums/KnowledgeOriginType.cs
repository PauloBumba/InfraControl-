namespace InfraControl.Domain.Enums;

/// <summary>
/// Origem do artigo Knowledge (F24-T004/K4, spec docs/32 §9 — "a
/// Knowledge mantém sua origem", com múltiplas origens). Guardado
/// como string.
///
/// A §9 lista também Projetos, documentos e treinamentos — essas
/// entidades não existem no repo, então não foram inventadas
/// (AGENTS.md: não inventar requisitos); se nascerem, entra valor
/// novo aqui + validação de existência no handler.
/// </summary>
public enum KnowledgeOriginType
{
    /// <summary>Origem em uma Task (TaskItem).</summary>
    Task,

    /// <summary>Origem em um PDI (IndividualDevelopmentPlan).</summary>
    Pdi,

    /// <summary>Origem em um Chamado/Incidente (§9 "Chamados").</summary>
    Incident,
}
