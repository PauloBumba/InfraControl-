namespace InfraControl.Application.Tasks;

/// <summary>TaskItem na leitura (F7-T001).</summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Kind,
    string State,
    DateTime OpenedAt,
    int AssigneeCount,
    Guid[] AssigneeIds,
    int DeviceCount,
    Guid[] DeviceIds,
    int IncidentCount,
    Guid[] IncidentIds)
{
    internal static TaskDto From(
        Domain.Entities.TaskItem task,
        IReadOnlyCollection<Guid>? assigneeIds = null,
        IReadOnlyCollection<Guid>? deviceIds = null,
        IReadOnlyCollection<Guid>? incidentIds = null)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Kind.ToString(),
            task.State.ToString(),
            task.CreateDateTime,
            assigneeIds?.Count ?? 0,
            assigneeIds?.ToArray() ?? [],
            deviceIds?.Count ?? 0,
            deviceIds?.ToArray() ?? [],
            incidentIds?.Count ?? 0,
            incidentIds?.ToArray() ?? []);
}
