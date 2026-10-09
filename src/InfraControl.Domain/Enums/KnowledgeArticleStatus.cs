namespace InfraControl.Domain.Enums;

/// <summary>
<<<<<<< HEAD
/// Estados do artigo Knowledge (F10-T003 + F24-T002/K2 — ADR-034,
/// spec docs/32 §7). Guardado como string (HasConversion) — linhas
/// antigas (Draft/Published/Archived) valem sem migração de dados.
///
/// Os números 0/1/2 são o contrato legado do F10-T002/T003 (o MVC não
/// registra JsonStringEnumConverter e o front trata esses valores);
/// os novos entram 3–7.
=======
/// Estados do artigo da base de conhecimento — workflow editorial de 8
/// estados (F24-T002/K2, ADR-039, docs/32 §7). Os 3 primeiros valores são
/// contrato antigo do F10-T003 e ficam fixos (o JSON expõe o enum como
/// número e o front usa constantes); os novos entram por append. As
/// transições vivem em <c>InfraControl.Domain.Entities.KnowledgeArticle.
/// TransitionTo</c>.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
/// </summary>
public enum KnowledgeArticleStatus
{
    /// <summary>Rascunho — nunca publicado; só quem escreve vê.</summary>
    Draft = 0,

    /// <summary>Publicada (viva).</summary>
    Published = 1,

    /// <summary>Arquivada — restaura para Draft.</summary>
    Archived = 2,
<<<<<<< HEAD

    /// <summary>Em revisão — feche em Approved (rascunho) ou Updated (publicado).</summary>
    InReview = 3,

    /// <summary>Aprovada — pronta para publicar.</summary>
    Approved = 4,

    /// <summary>Atualizada — publicada de volta após revisão (viva).</summary>
    Updated = 5,

    /// <summary>Obsoleta — conteúdo substituído; restaura para Draft.</summary>
    Obsolete = 6,

    /// <summary>Rejeitada — volta para Draft (retrabalho).</summary>
=======
    InReview = 3,
    Approved = 4,
    Updated = 5,
    Obsolete = 6,
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    Rejected = 7,
}
