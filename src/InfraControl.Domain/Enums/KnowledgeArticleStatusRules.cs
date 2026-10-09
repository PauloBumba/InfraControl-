namespace InfraControl.Domain.Enums;

/// <summary>
/// Regras de estado do artigo Knowledge (F24-T002/K2 — ADR-034 §4).
/// Fonte única dos estados **nunca publicados**: escondidos de quem não
/// tem nível de escrita na busca, e o <c>GET/{id}</c> responde 404
/// (não vaza existência — generalização da regra F10-T003).
/// A coleção fica pública porque o repositório a usa dentro da
/// expressão LINQ (<c>Contains</c> traduz para <c>IN</c> nos dois
/// providers) — a expressão precisa da referência, não de chamada
/// client-side.
/// </summary>
public static class KnowledgeArticleStatusRules
{
    /// <summary>Draft, InReview, Approved, Rejected — nunca publicados.</summary>
    public static readonly IReadOnlyCollection<KnowledgeArticleStatus> UnpublishedStatuses =
    [
        KnowledgeArticleStatus.Draft,
        KnowledgeArticleStatus.InReview,
        KnowledgeArticleStatus.Approved,
        KnowledgeArticleStatus.Rejected,
    ];

    /// <summary>Estado escondido de quem não escreve (ADR-034 §4).</summary>
    public static bool IsUnpublished(this KnowledgeArticleStatus status)
        => UnpublishedStatuses.Contains(status);
}
