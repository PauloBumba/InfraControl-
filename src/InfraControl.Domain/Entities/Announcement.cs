using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Anúncio da Central de Operações (ADR-063 F37-T002). Fila persistida
/// no backend para deduplicação, prioridade, escalonamento e reconhecimento.
/// Reproduzido no front via speechSynthesis/sons via SignalR.
/// </summary>
public sealed class Announcement : Entity
{
    public const int MessageMaxLength = 500;
    public const int FingerprintMaxLength = 255;

    public string Message { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "info"; // info, warning, critical
    public int Priority { get; private set; } = 0; // 0=baixa, 1=normal, 2=alta
    public int RepeatCount { get; private set; } = 0;
    public int MaxRepeats { get; private set; } = 3;
    public DateTime ScheduledAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public bool IsAcknowledged => AcknowledgedAt.HasValue;

    private Announcement() { }

    public static Announcement Create(
        string message,
        string fingerprint,
        string severity = "info",
        int priority = 0,
        int maxRepeats = 3,
        DateTime? scheduledAt = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A mensagem do anúncio é obrigatória.", nameof(message));

        if (message.Trim().Length > MessageMaxLength)
            throw new ArgumentException(
                $"A mensagem deve ter no máximo {MessageMaxLength} caracteres.", nameof(message));

        if (string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("O fingerprint (deduplicação) é obrigatório.", nameof(fingerprint));

        if (fingerprint.Trim().Length > FingerprintMaxLength)
            throw new ArgumentException(
                $"O fingerprint deve ter no máximo {FingerprintMaxLength} caracteres.", nameof(fingerprint));

        return new Announcement
        {
            Message = message.Trim(),
            Fingerprint = fingerprint.Trim(),
            Severity = severity.Trim().ToLowerInvariant(),
            Priority = priority,
            MaxRepeats = maxRepeats,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow
        };
    }

    public void Acknowledge()
    {
        if (IsAcknowledged) return;
        AcknowledgedAt = DateTime.UtcNow;
    }

    public void IncrementRepeat()
    {
        RepeatCount++;
    }
}
