using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Pausa do relógio de SLA de uma Task (F8-T004) — enquanto o SLA está
/// pausado (ex.: estados Waiting/Blocked), os vencimentos não correm.
/// `EndAt` null = pausa em aberto; ao fechar, a duração é somada aos
/// `*DueAt` do `TaskSla` (docs/04: prazos históricos preservados — os
/// minutos do snapshot não mudam).
/// </summary>
public sealed class TaskSlaPause : Entity
{
    public Guid TaskId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime? EndAt { get; private set; }

    private TaskSlaPause() { }

    public static TaskSlaPause Start(Guid taskId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));

        return new TaskSlaPause
        {
            TaskId = taskId,
            StartAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime,
            EndAt = null
        };
    }

    public bool IsOpen => EndAt is null;

    public void Close()
    {
        if (EndAt is not null)
            throw new InvalidOperationException("A pausa já está encerrada.");

        EndAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        if (EndAt < StartAt)
            EndAt = StartAt;
    }

    public TimeSpan Duration =>
        EndAt is null ? TimeSpan.Zero : EndAt.Value - StartAt;
}
