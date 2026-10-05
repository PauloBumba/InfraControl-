namespace InfraControl.Domain.Enums;

/// <summary>
/// Estado do PDI — ciclo de vida completo (docs/30 §3, ADR-029). A
/// cadeia padrão é Draft → Planned → AwaitingAcceptance → Active →
/// InProgress → UnderEvaluation → Completed, com os ramos
/// Replanned ⇄ Active e Active → Cancelled; transições válidas em
/// <c>Domain.Entities.PdiTransitions</c>. Armazenado como string
/// (<c>HasConversion&lt;string&gt;()</c>).
/// </summary>
public enum PdiStatus
{
    /// <summary>Rascunho — plano em construção pelo Admin.</summary>
    Draft = 1,

    /// <summary>Planejado — pronto para envio ao colaborador.</summary>
    Planned = 2,

    /// <summary>Aguardando aceite — colaborador decide começar.</summary>
    AwaitingAcceptance = 3,

    /// <summary>Ativo — aceito, em vigor.</summary>
    Active = 4,

    /// <summary>Em acompanhamento — execução/bilateral (era "em andamento").</summary>
    InProgress = 5,

    /// <summary>Em avaliação — autoavaliação/avaliação do gestor.</summary>
    UnderEvaluation = 6,

    /// <summary>Concluído — ciclo finalizado com resultado.</summary>
    Completed = 7,

    /// <summary>Replanejado — ramo de Ativo; retorna para Ativo.</summary>
    Replanned = 8,

    /// <summary>Cancelado — encerramento; só a partir de Ativo (docs/30 §3).</summary>
    Cancelled = 9
}
