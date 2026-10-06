using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Registro de progresso/comentário do colaborador na fase (docs/30
/// §9 "Relato Progresso", §23 PDIProgress; Fase 3 — ADR-033).
/// Append-only: o histórico de progresso é matéria-prima de indicadores
/// (§21), então não se sobrescreve — remover é folha (sem children).
/// </summary>
public sealed class PdiProgress : Entity
{
    public const int CommentMaxLength = 2000;

    public Guid PhaseId { get; private set; }
    public Guid AuthorId { get; private set; }

    /// <summary>Percentual relatado (opcional) — 0..100.</summary>
    public int? Percent { get; private set; }

    /// <summary>Comentário/relato do progresso (obrigatório).</summary>
    public string Comment { get; private set; } = string.Empty;

    private PdiProgress() { }

    public static PdiProgress Create(
        Guid phaseId, Guid authorId, int? percent, string comment)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase do progresso é obrigatória.", nameof(phaseId));
        if (authorId == Guid.Empty)
            throw new ArgumentException(
                "O autor do progresso é obrigatório.", nameof(authorId));

        var progress = new PdiProgress { PhaseId = phaseId, AuthorId = authorId };
        progress.Apply(percent, comment);
        return progress;
    }

    private void Apply(int? percent, string comment)
    {
        if (percent is { } value && value is < 0 or > 100)
            throw new ArgumentException(
                "O percentual do progresso deve estar entre 0 e 100.",
                nameof(percent));

        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException(
                "O comentário do progresso é obrigatório.", nameof(comment));

        Percent = percent;
        Comment = PdiCompetency.Normalize(
            comment, CommentMaxLength, nameof(comment))!;
        Touch();
    }
}
