namespace InfraControl.Domain.Enums;

/// <summary>
/// Origem do artigo Knowledge (F24-T004/K4, spec docs/32 §9 — "a
/// Knowledge mantém sua origem", com múltiplas origens). Guardado
/// como string.
///
/// O módulo de Projetos já foi incorporado ao tipo de origem e exige
/// validação de existência no handler; futuramente, outros tipos podem
/// entrar por extensão explícita do enum e do comando de vínculo.
/// </summary>
public enum KnowledgeOriginType
{
    /// <summary>Origem em uma Task (TaskItem).</summary>
    Task,

    /// <summary>Origem em um PDI (IndividualDevelopmentPlan).</summary>
    Pdi,

    /// <summary>Origem em um Projeto (Project).</summary>
    Project,

    /// <summary>Origem em um Chamado/Incidente (§9 "Chamados").</summary>
    Incident,
}
