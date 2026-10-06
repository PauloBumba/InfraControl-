using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Feedback da fase (docs/30 §13 — "histórico, não sobrescrever":
/// data, autor, destinatário, fase, tipo, conteúdo; §23 PDIFeedback;
/// Fase 3 — ADR-033). Sem operação de atualização por contrato; a
/// exclusão é restrita ao Admin (controller Administer + handler).
/// </summary>
public sealed class PdiFeedback : Entity
{
    public const int TypeMaxLength = 100;
    public const int ContentMaxLength = 2000;

    public Guid PhaseId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateOnly FeedbackDate { get; private set; }

    private PdiFeedback() { }

    public static PdiFeedback Create(
        Guid phaseId,
        Guid authorId,
        Guid recipientId,
        string type,
        string content,
        DateOnly feedbackDate)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase do feedback é obrigatória.", nameof(phaseId));
        if (authorId == Guid.Empty)
            throw new ArgumentException(
                "O autor do feedback é obrigatório.", nameof(authorId));
        if (recipientId == Guid.Empty)
            throw new ArgumentException(
                "O destinatário do feedback é obrigatório.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException(
                "O tipo do feedback é obrigatório.", nameof(type));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo do feedback é obrigatório.", nameof(content));

        return new PdiFeedback
        {
            PhaseId = phaseId,
            AuthorId = authorId,
            RecipientId = recipientId,
            Type = PdiCompetency.Normalize(type, TypeMaxLength, nameof(type))!,
            Content = PdiCompetency.Normalize(content, ContentMaxLength, nameof(content))!,
            FeedbackDate = feedbackDate
        };
    }
}
