namespace InfraControl.Domain.Enums;

/// <summary>
/// Resultado final do PDI — os 4 estados da comparação final
/// (docs/30 §16, ADR-036): atingido · parcialmente atingido · não
/// atingido · replanejado. <c>Replanejado</c> devolve o plano ao ciclo
/// (UnderEvaluation → Replanned → Active). Armazenado como string.
/// </summary>
public enum PdiResultOutcome
{
    /// <summary>Meta atingida no período.</summary>
    Atingido = 1,

    /// <summary>Progresso parcial — atingido em parte.</summary>
    ParcialmenteAtingido = 2,

    /// <summary>Sem atingir a meta do período.</summary>
    NaoAtingido = 3,

    /// <summary>Resultado = replanejar; o ciclo continua (§3 Replanned).</summary>
    Replanejado = 4
}

/// <summary>Rótulos pt-BR do resultado final (§16).</summary>
public static class PdiResultOutcomeExtensions
{
    public static string ToDisplayLabel(this PdiResultOutcome outcome)
        => outcome switch
        {
            PdiResultOutcome.Atingido => "Atingido",
            PdiResultOutcome.ParcialmenteAtingido => "Parcialmente atingido",
            PdiResultOutcome.NaoAtingido => "Não atingido",
            PdiResultOutcome.Replanejado => "Replanejado",
            _ => outcome.ToString()
        };
}
