namespace InfraControl.Application.Tasks.History;

/// <summary>Entrada do histórico da Task na leitura (F7-T006).</summary>
public sealed record TaskHistoryDto(
    Guid Id,
    Guid TaskId,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string? Detail,
    DateTime CreatedAt);
