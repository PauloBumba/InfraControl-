using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Artigo da base de conhecimento (F10-T002, docs/04 — Knowledge Base).
/// Nasce <see cref="KnowledgeArticleStatus.Draft"/>; as transições de
/// status vêm em F10-T003 (Publishing). Categoria opcional — a FK é
/// SetNull na migration: apagar categoria não apaga nem bloqueia o
/// artigo (decisão da task, T001 não tinha FK).
/// </summary>
public sealed class KnowledgeArticle : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 20_000;

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }

    private KnowledgeArticle() { }

    public static KnowledgeArticle Create(string title, string content, Guid? categoryId)
    {
        var article = new KnowledgeArticle
        {
            Status = KnowledgeArticleStatus.Draft,
        };
        article.Update(title, content, categoryId);
        return article;
    }

    /// <summary>
    /// Atualiza título/conteúdo/categoria. NÃO mexe no Status — transição
    /// de estado é responsabilidade de F10-T003 (Publishing).
    /// </summary>
    public void Update(string title, string content, Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título do artigo é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título do artigo pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo do artigo é obrigatório.", nameof(content));

        content = content.Trim();
        if (content.Length > ContentMaxLength)
            throw new ArgumentException(
                $"O conteúdo do artigo pode ter no máximo {ContentMaxLength} caracteres.",
                nameof(content));

        Title = title;
        Content = content;
        CategoryId = categoryId;
    }
}
