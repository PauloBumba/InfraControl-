namespace InfraControl.Domain.Enums;

/// <summary>
/// Ações registradas no histórico da Task (F7-T006). Persistido como
/// string(40); F7-T007 (Workflow) pode adicionar transições de estado.
/// </summary>
public enum TaskHistoryAction
{
    Created,
    Updated,
    Assigned,
    Unassigned,
    DeviceLinked,
    DeviceUnlinked,
    IncidentLinked,
    IncidentUnlinked,
    Commented
}
