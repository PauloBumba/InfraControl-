namespace InfraControl.Application.Tasks;

/// <summary>Comentário da Task na leitura (F7-T005).</summary>
public sealed record TaskCommentDto(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string? AuthorName,
    string Content,
    DateTime CreatedAt);
