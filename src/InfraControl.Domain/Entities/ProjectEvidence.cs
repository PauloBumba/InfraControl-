using System.IO;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Evidência técnica do Projeto (ADR-043 · docs/38 §3.4): metadados no
/// banco, binário em <c>var/project-evidence</c> (ADR-033/034).
/// </summary>
public sealed class ProjectEvidence : Entity
{
    public const int MaxDescriptionLength = 500;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 128;
    public const int MaxExtensionLength = 10;
    public const long MaxFileSize = 10 * 1024 * 1024;
    public const string DefaultContentType = "application/octet-stream";

    public Guid ProjectId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public ProjectEvidenceType Type { get; private set; }
    public string? Description { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = DefaultContentType;
    public long FileSize { get; private set; }
    public ProjectEvidenceStatus Status { get; private set; } = ProjectEvidenceStatus.Pending;
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>Nome no storage: <c>{Id}{extensão}</c>.</summary>
    public string StorageFileName => $"{Id}{Extension}";

    private ProjectEvidence() { }

    public static ProjectEvidence Create(
        Guid projectId,
        Guid createdBy,
        ProjectEvidenceType type,
        string? description,
        string fileName,
        string? contentType,
        long fileSize)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(
                "O projeto da evidência é obrigatório.", nameof(projectId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException(
                "O autor da evidência é obrigatório.", nameof(createdBy));
        if (!Enum.IsDefined(type))
            throw new ArgumentException(
                "O tipo de evidência é inválido.", nameof(type));

        var safeName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new ArgumentException(
                "O nome do arquivo é obrigatório.", nameof(fileName));
        if (safeName.Length > MaxFileNameLength)
            throw new ArgumentException(
                $"O nome do arquivo excede {MaxFileNameLength} caracteres.",
                nameof(fileName));

        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (extension.Length > MaxExtensionLength)
            throw new ArgumentException(
                "A extensão do arquivo é inválida.", nameof(fileName));

        if (fileSize <= 0)
            throw new ArgumentException(
                "O arquivo é obrigatório e não pode ser vazio.",
                nameof(fileSize));
        if (fileSize > MaxFileSize)
            throw new ArgumentException(
                $"O arquivo excede o tamanho máximo de {MaxFileSize / (1024 * 1024)} MB.",
                nameof(fileSize));

        var normalizedType = (contentType ?? string.Empty).Trim();
        if (normalizedType.Length == 0)
            normalizedType = DefaultContentType;
        if (normalizedType.Length > MaxContentTypeLength)
            throw new ArgumentException(
                $"O content type excede {MaxContentTypeLength} caracteres.",
                nameof(contentType));

        string? normalizedDescription = null;
        if (!string.IsNullOrWhiteSpace(description))
        {
            normalizedDescription = description.Trim();
            if (normalizedDescription.Length > MaxDescriptionLength)
                throw new ArgumentException(
                    $"A descrição pode ter no máximo {MaxDescriptionLength} caracteres.",
                    nameof(description));
        }

        return new ProjectEvidence
        {
            ProjectId = projectId,
            CreatedBy = createdBy,
            Type = type,
            Description = normalizedDescription,
            OriginalFileName = safeName,
            Extension = extension,
            ContentType = normalizedType,
            FileSize = fileSize
        };
    }

    public void ValidateBy(Guid reviewerId)
        => ReviewBy(reviewerId, ProjectEvidenceStatus.Validated, reason: null);

    public void RejectBy(Guid reviewerId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "O motivo da rejeição é obrigatório.", nameof(reason));

        ReviewBy(reviewerId, ProjectEvidenceStatus.Rejected, reason);
    }

    private void ReviewBy(Guid reviewerId, ProjectEvidenceStatus status, string? reason)
    {
        if (reviewerId == Guid.Empty)
            throw new ArgumentException(
                "O revisor da evidência é obrigatório.", nameof(reviewerId));
        if (Status != ProjectEvidenceStatus.Pending)
            throw new InvalidOperationException(
                "Esta evidência já foi revisada.");

        Status = status;
        ReviewedBy = reviewerId;
        ReviewedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        if (reason is null)
        {
            RejectionReason = null;
        }
        else
        {
            reason = reason.Trim();
            if (reason.Length > MaxDescriptionLength)
                throw new ArgumentException(
                    $"O motivo da rejeição pode ter no máximo {MaxDescriptionLength} caracteres.",
                    nameof(reason));
            RejectionReason = reason;
        }

        Touch();
    }
}
