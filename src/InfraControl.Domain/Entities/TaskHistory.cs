using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>Entrada do histórico de uma Task — quem fez o quê e quando (F7-T006).</summary>
public sealed class TaskHistory : Entity
{
    public const int DetailMaxLength = 200;

    public Guid TaskId { get; private set; }
    public Guid? ActorId { get; private set; }
    public TaskHistoryAction Action { get; private set; }
    public string? Detail { get; private set; }

    private TaskHistory() { }

    public static TaskHistory Create(
        Guid taskId,
        TaskHistoryAction action,
        Guid? actorId = null,
        string? detail = null)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));

        if (detail?.Length > DetailMaxLength)
            throw new ArgumentException(
                $"O detalhe pode ter no máximo {DetailMaxLength} caracteres.",
                nameof(detail));

        return new TaskHistory
        {
            TaskId = taskId,
            Action = action,
            ActorId = actorId is { } id && id == Guid.Empty ? null : actorId,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim()
        };
    }
}
