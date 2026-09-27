namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados da Task (docs/04). Nome `TaskState` (não `TaskStatus`)
/// evita colisão com `System.Threading.Tasks.TaskStatus`; transições
/// válidas são F7-T007 (Workflow).
/// </summary>
public enum TaskState
{
    Open,
    InProgress,
    Waiting,
    Blocked,
    Resolved,
    Cancelled,
    Closed
}
