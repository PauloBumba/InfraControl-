namespace InfraControl.Application.Tasks;

/// <summary>TaskItem na leitura (F7-T001).</summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Kind,
    string State,
    DateTime OpenedAt)
{
    internal static TaskDto From(Domain.Entities.TaskItem task)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Kind.ToString(),
            task.State.ToString(),
            task.CreateDateTime);
}
