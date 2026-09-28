namespace InfraControl.Domain.Enums;

/// <summary>
/// F7-T007: mapa de transições válidas entre estados da Task (docs/04).
/// Par fora do mapa → <see cref="InvalidOperationException"/> em
/// <c>TaskItem.ChangeState</c> (GlobalExceptionHandler → 409 pt-BR);
/// mesmo estado é no-op, não transição.
/// </summary>
public static class TaskStateWorkflow
{
    private static readonly IReadOnlyDictionary<TaskState, TaskState[]> Allowed =
        new Dictionary<TaskState, TaskState[]>
        {
            // Aberta: começar a execução ou cancelar direto.
            [TaskState.Open] = [TaskState.InProgress, TaskState.Cancelled],

            // Em execução: pausar, bloquear, concluir ou cancelar.
            [TaskState.InProgress] =
            [
                TaskState.Waiting, TaskState.Blocked,
                TaskState.Resolved, TaskState.Cancelled
            ],

            // Espera/bloqueio voltam para a execução (ou cancelam).
            [TaskState.Waiting] =
            [TaskState.InProgress, TaskState.Blocked, TaskState.Cancelled],
            [TaskState.Blocked] = [TaskState.InProgress, TaskState.Cancelled],

            // Resolvida fecha ou reabre (retrabalho).
            [TaskState.Resolved] = [TaskState.Closed, TaskState.InProgress],

            // Cancelada e fechada reabrem (reativação).
            [TaskState.Cancelled] = [TaskState.Open],
            [TaskState.Closed] = [TaskState.Open]
        };

    /// <summary>Transição permitida pelo mapa (estado diferente por definição).</summary>
    public static bool CanTransition(TaskState from, TaskState to)
        => from != to
           && Allowed.TryGetValue(from, out var targets)
           && Array.IndexOf(targets, to) >= 0;
}
