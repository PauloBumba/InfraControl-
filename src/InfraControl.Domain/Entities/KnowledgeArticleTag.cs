using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Tag do artigo da base de conhecimento (F24-T001/K1, docs/32 §6).
/// Vive no agregado <see cref="KnowledgeArticle"/>: a validação (limite
/// de 10, 30 chars, normalização lowercase/dedupe) é do agregado — aqui
/// só o par (article_id, tag) com chave composta.
/// </summary>
public sealed class KnowledgeArticleTag : Entity
{
    public Guid ArticleId { get; private set; }
    public string Tag { get; private set; } = string.Empty;

    private KnowledgeArticleTag() { }

    internal static KnowledgeArticleTag Create(Guid articleId, string tag) =>
        new() { ArticleId = articleId, Tag = tag };
}
