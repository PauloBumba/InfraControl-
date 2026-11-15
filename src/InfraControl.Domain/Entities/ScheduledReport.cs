using System.Text.Json;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Agenda do relatório geral por e-mail: o admin define a lista de
/// destinatários e a periodicidade; o ScheduledReportWorker dispara no
/// horário UTC agendado e envia o relatório a TODOS os destinatários.
/// Hoje suporta o kind "General" (visão geral da infraestrutura);
/// novos kinds entram sem mudar a agenda.
/// </summary>
public sealed class ScheduledReport : Entity
{
    public const int MaxRecipients = 20;
    public const int MaxEmailLength = 320;
    public const int MaxErrorLength = 1000;
    public const string GeneralKind = "General";

    /// <summary>Tipo de relatório ("General" — único suportado hoje).</summary>
    public string Kind { get; private set; } = GeneralKind;

    /// <summary>Destinatários serializados em JSON (coluna recipients_json).</summary>
    public string RecipientsJson { get; private set; } = "[]";

    public ReportFrequency Frequency { get; private set; } = ReportFrequency.Daily;

    /// <summary>Hora do envio em UTC (TimeOnly — persistido como time).</summary>
    public TimeOnly SendTimeUtc { get; private set; } = new(8, 0);

    /// <summary>Dia da semana para Weekly (0=domingo…6=sábado); null em Daily.</summary>
    public int? DayOfWeek { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastSentAtUtc { get; private set; }
    public DateTime? NextRunAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private ScheduledReport() { }

    public IReadOnlyList<string> Recipients =>
        JsonSerializer.Deserialize<List<string>>(RecipientsJson) ?? [];

    public static ScheduledReport Create(
        IReadOnlyList<string> recipients,
        ReportFrequency frequency,
        TimeOnly sendTimeUtc,
        int? dayOfWeek,
        bool isActive)
    {
        var report = new ScheduledReport();
        report.Update(recipients, frequency, sendTimeUtc, dayOfWeek, isActive);
        report.NextRunAtUtc = report.ComputeNextRun(
            AppTimeProvider.Current.GetUtcNow().UtcDateTime);
        return report;
    }

    public void Update(
        IReadOnlyList<string> recipients,
        ReportFrequency frequency,
        TimeOnly sendTimeUtc,
        int? dayOfWeek,
        bool isActive)
    {
        var normalized = NormalizeRecipients(recipients);
        if (normalized.Count == 0)
            throw new ArgumentException(
                "Informe ao menos um e-mail destinatário.", nameof(recipients));

        if (frequency == ReportFrequency.Weekly)
        {
            if (dayOfWeek is null or < 0 or > 6)
                throw new ArgumentException(
                    "Relatório semanal exige um dia da semana válido.", nameof(dayOfWeek));
        }
        else if (dayOfWeek is not null)
        {
            throw new ArgumentException(
                "Relatório diário não usa dia da semana.", nameof(dayOfWeek));
        }

        RecipientsJson = JsonSerializer.Serialize(normalized);
        Frequency = frequency;
        SendTimeUtc = sendTimeUtc;
        DayOfWeek = frequency == ReportFrequency.Weekly ? dayOfWeek : null;
        IsActive = isActive;

        // Reagenda a partir de agora — mudança de agenda não dispara envio retroativo.
        NextRunAtUtc = ComputeNextRun(AppTimeProvider.Current.GetUtcNow().UtcDateTime);
        LastError = null;
        Touch();
    }

    /// <summary>Marca envio bem-sucedido e calcula o próximo horário.</summary>
    public void MarkSent(DateTime sentAtUtc)
    {
        LastSentAtUtc = sentAtUtc;
        LastError = null;
        NextRunAtUtc = ComputeNextRun(sentAtUtc);
        Touch();
    }

    /// <summary>
    /// Falha de envio: reagenda com retry curto (15min) sem perder o slot —
    /// SMTP fora do ar não deve silenciar o relatório por um ciclo inteiro.
    /// </summary>
    public void MarkFailed(DateTime failedAtUtc, string error)
    {
        var trimmed = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        LastError = trimmed is { Length: > MaxErrorLength }
            ? trimmed[..MaxErrorLength]
            : trimmed;
        NextRunAtUtc = failedAtUtc.AddMinutes(15);
        Touch();
    }

    /// <summary>Próximo disparo estritamente após <paramref name="after"/> (UTC).</summary>
    public DateTime ComputeNextRun(DateTime afterUtc)
    {
        var after = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);

        if (Frequency == ReportFrequency.Daily)
        {
            var candidate = after.Date.Add(SendTimeUtc.ToTimeSpan());
            return candidate <= after ? candidate.AddDays(1) : candidate;
        }

        // Weekly: próximo DayOfWeek no SendTimeUtc (0–6).
        for (var i = 0; i < 8; i++)
        {
            var day = after.Date.AddDays(i);
            if ((int)day.DayOfWeek != DayOfWeek)
                continue;

            var candidate = day.Add(SendTimeUtc.ToTimeSpan());
            if (candidate > after)
                return candidate;
        }

        // Inalcançável (loop cobre 8 dias), fallback defensivo.
        return after.AddDays(7);
    }

    private static List<string> NormalizeRecipients(IReadOnlyList<string> recipients)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in recipients)
        {
            var email = raw?.Trim();
            if (string.IsNullOrEmpty(email))
                continue;

            if (email.Length > MaxEmailLength)
                throw new ArgumentException(
                    $"E-mail excede {MaxEmailLength} caracteres: '{email[..50]}…'.",
                    nameof(recipients));

            // Validação leve de formato — não rejeita endereços válidos
            // incomuns; o SMTP rejeita de verdade se for inválido.
            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
                throw new ArgumentException(
                    $"E-mail inválido: '{email}'.", nameof(recipients));

            if (seen.Add(email))
            {
                if (result.Count >= MaxRecipients)
                    throw new ArgumentException(
                        $"No máximo {MaxRecipients} destinatários.", nameof(recipients));
                result.Add(email);
            }
        }

        return result;
    }
}
