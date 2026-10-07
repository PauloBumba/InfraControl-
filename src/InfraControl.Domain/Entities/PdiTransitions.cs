using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Tabela de transições do ciclo de vida do PDI — setas exatas do
/// diagrama docs/30 §3 (ADR-029). O domínio valida toda mudança de
/// status por este mapa; transição fora da tabela é
/// <see cref="ArgumentException"/> (400 pt-BR). Mesmo estado é sempre
/// permitido (edição de campos sem transição).
/// </summary>
public static class PdiTransitions
{
    private static readonly IReadOnlyDictionary<PdiStatus, PdiStatus[]> Allowed =
        new Dictionary<PdiStatus, PdiStatus[]>
        {
            [PdiStatus.Draft] = [PdiStatus.Planned],
            [PdiStatus.Planned] = [PdiStatus.AwaitingAcceptance],
            [PdiStatus.AwaitingAcceptance] = [PdiStatus.Active],
            [PdiStatus.Active] =
            [
                PdiStatus.InProgress,
                PdiStatus.Replanned,
                PdiStatus.Cancelled
            ],
            [PdiStatus.InProgress] = [PdiStatus.UnderEvaluation],
            // ADR-036 (Fase 6): a avaliação final (§16) termina em
            // Concluído — ou devolve o plano ao ciclo quando o
            // resultado é "Replanejado".
            [PdiStatus.UnderEvaluation] =
            [
                PdiStatus.Completed,
                PdiStatus.Replanned
            ],
            [PdiStatus.Replanned] = [PdiStatus.Active],
            [PdiStatus.Completed] = [],
            [PdiStatus.Cancelled] = []
        };

    /// <summary>Próximos estados válidos a partir de <paramref name="from"/>.</summary>
    public static IReadOnlyCollection<PdiStatus> Next(PdiStatus from)
        => Allowed.TryGetValue(from, out var next)
            ? next
            : [];

    /// <summary>Transição permitida (ou ausência de mudança).</summary>
    public static bool CanMove(PdiStatus from, PdiStatus to)
        => from == to || Next(from).Contains(to);

    /// <summary>Valida a transição; lança 400 pt-BR quando inválida.</summary>
    public static void EnsureCanMove(PdiStatus from, PdiStatus to)
    {
        if (CanMove(from, to))
            return;

        throw new ArgumentException(
            $"Transição de status inválida: {from} → {to}.",
            nameof(to));
    }
}
