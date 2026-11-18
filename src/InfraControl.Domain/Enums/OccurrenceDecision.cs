namespace InfraControl.Domain.Enums;

/// <summary>
/// Classificação da decisão do OccurrenceTransitionGate (auditoria §3):
/// cada avaliação fica registrada — aceitas viram transição+notificação,
/// descartadas ficam auditáveis sem gerar comunicação.
/// </summary>
public enum OccurrenceDecision
{
    /// <summary>Mudança de estado válida — notificação autorizada.</summary>
    TransitionAccepted = 0,
    /// <summary>Estado recebido igual ao persistido — descartado.</summary>
    SameState = 1,
    /// <summary>Corrida de decisão — estado reavaliado após conflito.</summary>
    ConcurrentConflict = 2,
    /// <summary>Evento já processado (inbox) — reservado; o gate não vê replay.</summary>
    DuplicateEvent = 3,
    /// <summary>Transição não permitida pelo domínio — reservado.</summary>
    InvalidTransition = 4
}
