namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos de Knowledge (spec docs/32 §5 — F24-T001/K1): os 14 tipos da
/// base. Guardado como string (<c>HasConversion&lt;string&gt;()</c>) —
/// acrescentar tipo novo não toca migração; o rótulo pt-BR espelhado
/// vive no catálogo do front (<c>knowledgeCatalogs.ts</c>).
/// Valor nulo no artigo = ainda não classificado (a spec não define
/// tipo padrão para os artigos já existentes).
/// </summary>
public enum KnowledgeArticleType
{
    /// <summary>Procedimento.</summary>
    Procedure,

    /// <summary>Manual.</summary>
    Manual,

    /// <summary>Guia.</summary>
    Guide,

    /// <summary>FAQ.</summary>
    Faq,

    /// <summary>Solução conhecida.</summary>
    KnownSolution,

    /// <summary>Configuração.</summary>
    Configuration,

    /// <summary>Documentação técnica.</summary>
    TechnicalDocumentation,

    /// <summary>Boas práticas.</summary>
    BestPractices,

    /// <summary>Política.</summary>
    Policy,

    /// <summary>Norma interna.</summary>
    InternalStandard,

    /// <summary>Troubleshooting.</summary>
    Troubleshooting,

    /// <summary>Conhecimento de sistema.</summary>
    SystemKnowledge,

    /// <summary>Conhecimento de infraestrutura.</summary>
    InfrastructureKnowledge,

    /// <summary>Conhecimento operacional.</summary>
    OperationalKnowledge,
}
