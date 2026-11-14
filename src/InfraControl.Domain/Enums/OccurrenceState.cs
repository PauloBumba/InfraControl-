namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados de uma ocorrência (spec de transições de notificação):
/// Alert e Incident são não-resolvidos; Resolved é o fechamento.
/// Usado pelo gate central de transição — só Alert/Incident/Resolved
/// disparam decisão de notificação.
/// </summary>
public enum OccurrenceState
{
    Alert = 0,
    Incident = 1,
    Resolved = 2
}
