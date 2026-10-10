using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Relato de progresso do Projeto (ADR-043 · docs/38 §3.4) — append-only,
/// espelho de <c>PdiProgress</c>.
/// </summary>
public sealed class ProjectProgress : Entity
{
    public const int CommentMaxLength = 2000;

    public Guid ProjectId { get; private set; }
    public Guid AuthorId { get; private set; }

    /// <summary>Percentual global reportado (opcional) — 0..100.</summary>
    public int? Percent { get; private set; }

    public string Comment { get; private set; } = string.Empty;

    private ProjectProgress() { }

    public static ProjectProgress Create(
        Guid projectId, Guid authorId, int? percent, string comment)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(
                "O projeto do progresso é obrigatório.", nameof(projectId));
        if (authorId == Guid.Empty)
            throw new ArgumentException(
                "O autor do progresso é obrigatório.", nameof(authorId));

        if (percent is { } value && value is < 0 or > 100)
            throw new ArgumentException(
                "O percentual do progresso deve estar entre 0 e 100.",
                nameof(percent));

        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException(
                "O comentário do progresso é obrigatório.", nameof(comment));

        comment = comment.Trim();
        if (comment.Length > CommentMaxLength)
            throw new ArgumentException(
                $"O comentário do progresso pode ter no máximo {CommentMaxLength} caracteres.",
                nameof(comment));

        return new ProjectProgress
        {
            ProjectId = projectId,
            AuthorId = authorId,
            Percent = percent,
            Comment = comment
        };
    }
}
