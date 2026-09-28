using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T006: writer em memória — captura as entradas gravadas.</summary>
public sealed class RecordingTaskHistoryWriter : ITaskHistoryWriter
{
    public List<(Guid TaskId, TaskHistoryAction Action, string? Detail)> Records { get; } = [];

    public Task RecordAsync(
        Guid taskId,
        TaskHistoryAction action,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        Records.Add((taskId, action, detail));
        return Task.CompletedTask;
    }
}
