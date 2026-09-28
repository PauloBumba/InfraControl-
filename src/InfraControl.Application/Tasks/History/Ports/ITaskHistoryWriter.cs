using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.History.Ports;

/// <summary>
/// Grava entradas no histórico da Task (F7-T006) — o actor é o usuário
/// logado (ICurrentUserContext), capturado aqui para não poluir os handlers.
/// </summary>
public interface ITaskHistoryWriter
{
    Task RecordAsync(
        Guid taskId,
        TaskHistoryAction action,
        string? detail = null,
        CancellationToken cancellationToken = default);
}
