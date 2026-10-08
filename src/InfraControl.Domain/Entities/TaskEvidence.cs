using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Evidência anexada a uma Task (F23-T004/P4c — Plano de Execução §11):
/// arquivo em disco local (ADR-033) com metadados na tabela. O nome
/// original é só metadado — em disco o arquivo vive como GUID +
/// extensão validada (allowlist), nunca com caminho do usuário.
/// </summary>
public sealed class TaskEvidence : AggregateRoot
{
    public const int FileNameMaxLength = 200;
    public const int ContentTypeMaxLength = 100;
    public const int NoteMaxLength = 300;

    /// <summary>ADR-033: 10 MB por arquivo (400 pt-BR acima disso).</summary>
    public const int MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Allowlist de extensões (ADR-033) — fora daqui = 400.</summary>
    public static readonly string[] AllowedExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf",
        ".txt", ".log", ".csv", ".json", ".xml", ".zip",
    ];

    public Guid TaskId { get; private set; }

    /// <summary>Nome original exibido ao usuário (nunca usado como caminho).</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Nome em disco: GUID + extensão validada (sem separadores).</summary>
    public string StoredName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? Note { get; private set; }

    private TaskEvidence() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskEvidenceDeletedDomainEvent(Id, TaskId));

    public static TaskEvidence Create(
        Guid taskId,
        string fileName,
        string? contentType,
        long fileSize,
        string? note = null)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException(
                "O id da task é obrigatório.", nameof(taskId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(
                "O nome do arquivo é obrigatório.", nameof(fileName));

        // Normaliza: browsers antigos mandam caminho junto — fica só o
        // último segmento (o nome vira metadado, nunca caminho: ADR-033).
        fileName = fileName.Trim().Replace('\\', '/');
        var slash = fileName.LastIndexOf('/');
        if (slash >= 0)
            fileName = fileName[(slash + 1)..].Trim();

        if (fileName.Length == 0)
            throw new ArgumentException(
                "O nome do arquivo é obrigatório.", nameof(fileName));
        if (fileName.Length > FileNameMaxLength)
            throw new ArgumentException(
                $"O nome do arquivo pode ter no máximo {FileNameMaxLength} caracteres.",
                nameof(fileName));

        var dot = fileName.LastIndexOf('.');
        var extension = dot >= 0 ? fileName[dot..].ToLowerInvariant() : string.Empty;
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Tipo de arquivo não permitido. Aceitos: {string.Join(", ", AllowedExtensions)}.",
                nameof(fileName));

        if (fileSize <= 0)
            throw new ArgumentException("O arquivo está vazio.", nameof(fileSize));
        if (fileSize > MaxFileSizeBytes)
            throw new ArgumentException(
                $"O arquivo pode ter no máximo {MaxFileSizeBytes / (1024 * 1024)} MB.",
                nameof(fileSize));

        if (note?.Length > NoteMaxLength)
            throw new ArgumentException(
                $"A nota pode ter no máximo {NoteMaxLength} caracteres.",
                nameof(note));

        var evidence = new TaskEvidence
        {
            TaskId = taskId,
            FileName = fileName,
            StoredName = Guid.NewGuid().ToString("N") + extension,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType.Trim(),
            FileSize = fileSize,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        if (evidence.ContentType.Length > ContentTypeMaxLength)
            throw new ArgumentException(
                $"O tipo de conteúdo pode ter no máximo {ContentTypeMaxLength} caracteres.",
                nameof(contentType));

        evidence.RaiseDomainEvent(new TaskEvidenceCreatedDomainEvent(
            evidence.Id, evidence.TaskId));
        return evidence;
    }
}
