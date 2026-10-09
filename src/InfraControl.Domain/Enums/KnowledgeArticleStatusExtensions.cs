namespace InfraControl.Domain.Enums;

/// <summary>
/// F24-T002/K2 (ADR-039): rótulos pt-BR e regra de visibilidade dos
/// estados do artigo. O front espelha os rótulos em
/// <c>components/knowledge/knowledgeCatalogs.ts</c>.
/// </summary>
public static class KnowledgeArticleStatusExtensions
{
    /// <summary>Estados ocultos de quem não tem nível de escrita
    /// (<c>Operate</c>): não publicados ou reprovados — a busca filtra e o
    /// <c>GET /{id}</c> responde 404 sem vazar a existência.</summary>
    public static readonly IReadOnlyCollection<KnowledgeArticleStatus> HiddenFromReaders =
    [
        KnowledgeArticleStatus.Draft,
        KnowledgeArticleStatus.InReview,
        KnowledgeArticleStatus.Approved,
        KnowledgeArticleStatus.Rejected,
    ];

    /// <summary>Visível para leitores = já publicado (ou histórico público).</summary>
    public static bool IsVisibleToReaders(this KnowledgeArticleStatus status)
        => !HiddenFromReaders.Contains(status);

    /// <summary>Rótulo pt-BR do estado — mensagens de 409 e leitura de
    /// logs (valores desconhecidos caem no nome do membro).</summary>
    public static string ToPtBrLabel(this KnowledgeArticleStatus status)
        => status switch
        {
            KnowledgeArticleStatus.Draft => "Rascunho",
            KnowledgeArticleStatus.Published => "Publicada",
            KnowledgeArticleStatus.Archived => "Arquivada",
            KnowledgeArticleStatus.InReview => "Em revisão",
            KnowledgeArticleStatus.Approved => "Aprovada",
            KnowledgeArticleStatus.Updated => "Atualizada",
            KnowledgeArticleStatus.Obsolete => "Obsoleta",
            KnowledgeArticleStatus.Rejected => "Rejeitada",
            _ => status.ToString(),
        };
}
