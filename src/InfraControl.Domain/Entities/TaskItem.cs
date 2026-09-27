using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Unidade genérica de trabalho (docs/04 — F7-T001). Nome `TaskItem`
/// (não `Task`) evita a colisão com `System.Threading.Tasks.Task` em
/// todo o codebase async. Nasce em <see cref="TaskState.Open"/>;
/// relacionamentos (assignees/devices/incidents), comentários, histórico
/// e workflow de transição vêm em F7-T002→T007.
/// </summary>
public sealed class TaskItem : AggregateRoot
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskKind Kind { get; private set; }
    public TaskState State { get; private set; }

    private TaskItem() { }

    public static TaskItem Create(
        string title,
        TaskKind kind,
        string? description = null)
    {
        var task = new TaskItem();
        task.Update(title, description);
        task.Kind = kind;
        task.State = TaskState.Open;
        return task;
    }

    public void Update(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da task é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título da task pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da task pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>
    /// F7-T007 (Workflow) valida as transições; aqui é no-op se o estado
    /// já é o alvo (sem "transição para o mesmo estado").
    /// </summary>
    public void ChangeState(TaskState state)
    {
        if (State == state)
            return;

        State = state;
    }
}
