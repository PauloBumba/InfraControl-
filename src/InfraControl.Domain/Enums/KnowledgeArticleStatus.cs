namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados do artigo da base de conhecimento — workflow editorial de 8
/// estados (F24-T002/K2, ADR-039, docs/32 §7). Os 3 primeiros valores são
/// contrato antigo do F10-T003 e ficam fixos (o JSON expõe o enum como
/// número e o front usa constantes); os novos entram por append. As
/// transições vivem em <c>InfraControl.Domain.Entities.KnowledgeArticle.
/// TransitionTo</c>.
/// </summary>
public enum KnowledgeArticleStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
    InReview = 3,
    Approved = 4,
    Updated = 5,
    Obsolete = 6,
    Rejected = 7,
}
