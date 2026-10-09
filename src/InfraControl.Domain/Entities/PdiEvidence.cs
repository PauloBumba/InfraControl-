using System.IO;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Evidência genérica sob a fase (docs/30 §12, §23 PDIEvidence;
/// Fase 4 — ADR-034): o colaborador envia um arquivo com tipo
/// classificado (9 tipos fixos), fica pendente até o gestor validar ou
/// rejeitar (motivo obrigatório). O arquivo mora no file store
/// (<see cref="StorageFileName"/>), a entidade guarda só metadados.
/// </summary>
public sealed class PdiEvidence : Entity
{
    public const int MaxDescriptionLength = 500;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 128;
    public const int MaxExtensionLength = 10;

    /// <summary>Limite do upload (docs/30 §12 — 10 MB).</summary>
    public const long MaxFileSize = 10 * 1024 * 1024;

    public const string DefaultContentType = "application/octet-stream";

    public Guid PhaseId { get; private set; }
    public Guid CreatedBy { get; private set; }

    public PdiEvidenceType Type { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Nome original (saneado — só o basename chega aqui).</summary>
    public string OriginalFileName { get; private set; } = string.Empty;

    /// <summary>Nome no storage: <c>{Id}{extensão}</c> (sem colisão possível).</summary>
    public string StorageFileName => $"{Id}{Extension}";

    /// <summary>Ponto final da extensão (ex.: <c>.pdf</c>) ou vazio.</summary>
    public string Extension { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = DefaultContentType;
    public long FileSize { get; private set; }

    public PdiEvidenceStatus Status { get; private set; } = PdiEvidenceStatus.Pending;

    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private PdiEvidence() { }

    public static PdiEvidence Create(
        Guid phaseId,
        Guid createdBy,
        PdiEvidenceType type,
        string? description,
        string fileName,
        string? contentType,
        long fileSize)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase da evidência é obrigatória.", nameof(phaseId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException(
                "O autor da evidência é obrigatório.", nameof(createdBy));
        if (!Enum.IsDefined(type))
            throw new ArgumentException(
                "O tipo de evidência é inválido.", nameof(type));

        // Saneamento: só o basename viaja — "pasta/cert.pdf" → "cert.pdf".
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

        return new PdiEvidence
        {
            PhaseId = phaseId,
            CreatedBy = createdBy,
            Type = type,
            Description = PdiCompetency.Normalize(
                description, MaxDescriptionLength, nameof(description)),
            OriginalFileName = safeName,
            Extension = extension,
            ContentType = normalizedType,
            FileSize = fileSize
        };
    }

    /// <summary>Gestor/Admin valida a evidência pendente (§12).</summary>
    public void ValidateBy(Guid reviewerId)
        => ReviewBy(reviewerId, PdiEvidenceStatus.Validated, reason: null);

    /// <summary>Gestor/Admin rejeita a evidência pendente — motivo obrigatório.</summary>
    public void RejectBy(Guid reviewerId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "O motivo da rejeição é obrigatório.", nameof(reason));

        ReviewBy(reviewerId, PdiEvidenceStatus.Rejected, reason);
    }

    private void ReviewBy(Guid reviewerId, PdiEvidenceStatus status, string? reason)
    {
        if (reviewerId == Guid.Empty)
            throw new ArgumentException(
                "O revisor da evidência é obrigatório.", nameof(reviewerId));
        if (Status != PdiEvidenceStatus.Pending)
            throw new InvalidOperationException(
                "Esta evidência já foi revisada.");

        Status = status;
        ReviewedBy = reviewerId;
        ReviewedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        RejectionReason = PdiCompetency.Normalize(
            reason, MaxDescriptionLength, nameof(reason));
    }
}
