namespace InfraControl.Domain.Enums;

/// <summary>
/// Status da fase de um Projeto (ADR-043 · docs/38 §3.2).
/// </summary>
public enum ProjectPhaseStatus
{
    /// <summary>Fase ainda não iniciada.</summary>
    NotStarted = 0,

    /// <summary>Fase em execução ativa pelas equipes técnicas.</summary>
    InProgress = 1,

    /// <summary>Entregáveis concluídos, aguardando validação dos critérios de aceite.</summary>
    PendingValidation = 2,

    /// <summary>Fase homologada e concluída com critérios atendidos.</summary>
    Completed = 3,
}
