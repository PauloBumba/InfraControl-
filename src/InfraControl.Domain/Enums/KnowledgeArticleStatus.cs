namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados do artigo Knowledge (F10-T003 + F24-T002/K2 — ADR-034,
/// spec docs/32 §7). Guardado como string (HasConversion) — linhas
/// antigas (Draft/Published/Archived) valem sem migração de dados.
///
/// Os números 0/1/2 são o contrato legado do F10-T002/T003 (o MVC não
/// registra JsonStringEnumConverter e o front trata esses valores);
/// os novos entram 3–7.
/// </summary>
public enum KnowledgeArticleStatus
{
    /// <summary>Rascunho — nunca publicado; só quem escreve vê.</summary>
    Draft = 0,

    /// <summary>Publicada (viva).</summary>
    Published = 1,

    /// <summary>Arquivada — restaura para Draft.</summary>
    Archived = 2,

    /// <summary>Em revisão — feche em Approved (rascunho) ou Updated (publicado).</summary>
    InReview = 3,

    /// <summary>Aprovada — pronta para publicar.</summary>
    Approved = 4,

    /// <summary>Atualizada — publicada de volta após revisão (viva).</summary>
    Updated = 5,

    /// <summary>Obsoleta — conteúdo substituído; restaura para Draft.</summary>
    Obsolete = 6,

    /// <summary>Rejeitada — volta para Draft (retrabalho).</summary>
    Rejected = 7,
}
