namespace InfraControl.Domain.Enums;

/// <summary>
/// Ciclo de vida de um Projeto (ADR-043 · docs/38 §4).
/// </summary>
public enum ProjectStatus
{
    /// <summary>Planejamento inicial / elaboração do escopo.</summary>
    Draft = 0,

    /// <summary>Planejado e aguardando kickoff formal.</summary>
    Planned = 1,

    /// <summary>Em execução ativa das fases e entregas.</summary>
    InProgress = 2,

    /// <summary>Fases concluídas, submetido para homologação formal pelo Administrador.</summary>
    PendingFinalApproval = 3,

    /// <summary>Homologado e aprovado pelo Administrador (estado terminal; dispara Knowledge Base).</summary>
    Completed = 4,

    /// <summary>Cancelado com justificativa obrigatória.</summary>
    Cancelled = 5,

    /// <summary>Pausado temporariamente com motivo registrado.</summary>
    OnHold = 6,
}
