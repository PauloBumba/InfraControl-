using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Snapshot imutável de uma versão do artigo (F24-T003/K3, spec
/// docs/32 §8 — "uma Knowledge nunca perde versões anteriores").
/// Cada versão registra os 5 campos da §8: **alteração** (o próprio
/// snapshot: título/conteúdo/descrição/tipo/tags/categoria), **autor**
/// (<see cref="AuthorId"/>), **data** (<see cref="CreateDateTime"/>),
/// **motivo** (<see cref="Reason"/>) e **aprovador**
/// (<see cref="ApprovedById"/> — carimbado na aprovação, K2/ADR-034).
///
/// A numeração vive no artigo (<c>KnowledgeArticle.BumpVersion</c>):
/// criação = 1.0, cada edição abre minor (1.1, 1.2…) e a partir de uma
/// aprovação a próxima edição abre novo major (2.0, 3.0…). Linha nunca
/// é editada depois de criada — o carimbo de aprovação é a única
/// mutação (nullable, só o primeiro aprovador vale).
/// </summary>
public sealed class KnowledgeArticleVersion : Entity
{
    public const int VersionMaxLength = 16;
    public const int ReasonMaxLength = 500;

    public Guid ArticleId { get; private set; }
    public string Version { get; private set; } = string.Empty;

    // Snapshot do documento (a "alteração" da §8).
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public KnowledgeArticleType? Type { get; private set; }
    public Guid? CategoryId { get; private set; }
    private List<string>? _tags;
    public IReadOnlyList<string> Tags => _tags ?? [];

    // Metadados da §8.
    public string Reason { get; private set; } = string.Empty;
    public Guid? AuthorId { get; private set; }
    public Guid? ApprovedById { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    private KnowledgeArticleVersion() { }

    /// <summary>
    /// Tira o snapshot do artigo **na versão atual dele** — quem chama
    /// garante que <c>BumpVersion()</c> já rodou (edição) ou que é a
    /// 1.0 (criação). As tags são copiadas: o artigo segue vivo.
    /// </summary>
    public static KnowledgeArticleVersion SnapshotOf(
        KnowledgeArticle article, string reason, Guid? authorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "O motivo da alteração é obrigatório.", nameof(reason));

        reason = reason.Trim();
        if (reason.Length > ReasonMaxLength)
            throw new ArgumentException(
                $"O motivo da alteração pode ter no máximo {ReasonMaxLength} caracteres.",
                nameof(reason));

        return new KnowledgeArticleVersion
        {
            ArticleId = article.Id,
            Version = article.Version,
            Title = article.Title,
            Content = article.Content,
            Description = article.Description,
            Type = article.Type,
            CategoryId = article.CategoryId,
            _tags = [.. article.Tags],
            Reason = reason,
            AuthorId = authorId,
        };
    }

    /// <summary>
    /// Carimbo de aprovação (K2/ADR-034): só o primeiro aprovador
    /// vale — re-aprovações não sobrescrevem a autoria da aprovação.
    /// </summary>
    public void Approve(Guid? approverId)
    {
        if (ApprovedById is not null)
            return;

        ApprovedById = approverId;
        ApprovedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }
}
