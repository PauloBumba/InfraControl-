namespace InfraControl.Application.Tasks;

/// <summary>
/// Entrada da timeline unificada da Task (F23-T004/P4d — §8): União de
/// histórico e comentários em leitura, tipada. `Id` é opaco e prefixado
/// ("h:" histórico | "c:" comentário) para desempate estável.
/// </summary>
public sealed record TaskTimelineEntryDto(
    string Id,
    string Type,
    string? Action,
    string? Detail,
    string? Comment,
    DateTime At,
    Guid? ActorId,
    string? ActorName);
