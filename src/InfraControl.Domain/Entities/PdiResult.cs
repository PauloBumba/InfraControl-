using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Resultado final do PDI (docs/30 §16 e §23 <c>PDIResult</c>,
/// ADR-036): 1 por plano (índice único em PlanId) com o desfecho dos
/// 4 estados do §16 + nota qualitativa opcional, carimbo de quem
/// finalizou e quando. Criado pelo gestor/Admin depois das duas
/// avaliações (§15.3) — conclui o plano ou o devolve ao ciclo
/// (Replanejado).
/// </summary>
public sealed class PdiResult : Entity
{
    public const int MaxNotesLength = 4000;

    public Guid PlanId { get; private set; }

    public PdiResultOutcome Outcome { get; private set; }

    /// <summary>Nota qualitativa opcional (§16 — "pode existir").</summary>
    public string? Notes { get; private set; }

    public Guid FinalizedById { get; private set; }

    public DateOnly FinalizedAt { get; private set; }

    private PdiResult() { }

    public static PdiResult Create(
        Guid planId,
        PdiResultOutcome outcome,
        string? notes,
        Guid finalizedById)
    {
        if (planId == Guid.Empty)
            throw new ArgumentException(
                "O plano do resultado é obrigatório.", nameof(planId));
        if (!Enum.IsDefined(outcome))
            throw new ArgumentException(
                "Resultado inválido (use Atingido, ParcialmenteAtingido, " +
                "NaoAtingido ou Replanejado).",
                nameof(outcome));
        if (finalizedById == Guid.Empty)
            throw new ArgumentException(
                "Quem finaliza o resultado é obrigatório.",
                nameof(finalizedById));

        if (notes is not null)
        {
            notes = notes.Trim();
            if (notes.Length == 0)
                notes = null;
            else if (notes.Length > MaxNotesLength)
                throw new ArgumentException(
                    $"A nota do resultado deve ter no máximo " +
                    $"{MaxNotesLength} caracteres.",
                    nameof(notes));
        }

        return new PdiResult
        {
            PlanId = planId,
            Outcome = outcome,
            Notes = notes,
            FinalizedById = finalizedById,
            FinalizedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }
}
