namespace InfraControl.Application.Tasks;

/// <summary>TaskEvidenceDto — evidência anexada (F23-T004/P4c — §11).</summary>
public sealed record TaskEvidenceDto(
    Guid Id,
    Guid TaskId,
    string FileName,
    string ContentType,
    long FileSize,
    string? Note,
    DateTime UploadedAt);
