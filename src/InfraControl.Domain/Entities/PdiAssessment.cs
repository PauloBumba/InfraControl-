using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Avaliação do PDI (docs/30 §15.3 e §23 <c>PDIAssessment</c>,
/// ADR-036): uma linha por lado do plano — autoavaliação do
/// colaborador (<see cref="PdiAssessmentSide.Self"/>) e avaliação do
/// gestor (<see cref="PdiAssessmentSide.Manager"/>) — conteúdo
/// qualitativo (§16 "avaliação qualitativa"), FK <c>Restrict</c> para
/// o plano e índice único (PlanId, Side). Salvar de novo atualiza a
/// própria linha (upsert); o resultado final congela a avaliação.
/// </summary>
public sealed class PdiAssessment : Entity
{
    public const int MaxContentLength = 4000;

    public Guid PlanId { get; private set; }

    public PdiAssessmentSide Side { get; private set; }

    /// <summary>Quem escreveu (dono para Self; gestor/Admin para Manager).</summary>
    public Guid AuthorId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    private PdiAssessment() { }

    public static PdiAssessment Create(
        Guid planId, PdiAssessmentSide side, Guid authorId, string content)
    {
        if (planId == Guid.Empty)
            throw new ArgumentException(
                "O plano da avaliação é obrigatório.", nameof(planId));
        if (!Enum.IsDefined(side))
            throw new ArgumentException(
                "Lado da avaliação inválido (use Self ou Manager).",
                nameof(side));
        if (authorId == Guid.Empty)
            throw new ArgumentException(
                "O autor da avaliação é obrigatório.", nameof(authorId));

        return new PdiAssessment
        {
            PlanId = planId,
            Side = side,
            AuthorId = authorId,
            Content = RequireContent(content)
        };
    }

    public void Update(string content)
    {
        Content = RequireContent(content);
        Touch();
    }

    private static string RequireContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo da avaliação é obrigatório.", nameof(content));

        content = content.Trim();
        if (content.Length > MaxContentLength)
            throw new ArgumentException(
                $"O conteúdo da avaliação deve ter no máximo " +
                $"{MaxContentLength} caracteres.",
                nameof(content));
        return content;
    }
}
