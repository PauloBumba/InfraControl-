using InfraControl.Application.Tasks.History.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.History;

/// <summary>F7-T006: grava histórico com o usuário logado como actor.</summary>
public sealed class TaskHistoryWriter(
    ITaskHistoryRepository repository,
    ICurrentUserContext currentUser,
    ILogger<TaskHistoryWriter> logger) : ITaskHistoryWriter
{
    public async Task RecordAsync(
        Guid taskId,
        TaskHistoryAction action,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var entry = TaskHistory.Create(
            taskId, action, currentUser.UserId, detail);

        await repository.AddAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Task {TaskId} history: {Action} ({ActorId}).",
            taskId, action, entry.ActorId);
    }
}
