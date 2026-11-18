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

    /// <summary>Dia da semana para Weekly (0=domingo…6=sábado); null em Daily/Monthly.</summary>
    public int? DayOfWeek { get; private set; }

    /// <summary>Dia do mês para Monthly (1–31; mês menor usa o último dia); null nos demais.</summary>
    public int? DayOfMonth { get; private set; }

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
        int? dayOfMonth,
        bool isActive)
    {
        var report = new ScheduledReport();
        report.Update(recipients, frequency, sendTimeUtc, dayOfWeek, dayOfMonth, isActive);
        report.NextRunAtUtc = report.ComputeNextRun(
            AppTimeProvider.Current.GetUtcNow().UtcDateTime);
        return report;
    }

    public void Update(
        IReadOnlyList<string> recipients,
        ReportFrequency frequency,
        TimeOnly sendTimeUtc,
        int? dayOfWeek,
        int? dayOfMonth,
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
                "Somente relatório semanal usa dia da semana.", nameof(dayOfWeek));
        }

        if (frequency == ReportFrequency.Monthly)
        {
            if (dayOfMonth is null or < 1 or > 31)
                throw new ArgumentException(
                    "Relatório mensal exige um dia do mês válido (1–31).", nameof(dayOfMonth));
        }
        else if (dayOfMonth is not null)
        {
            throw new ArgumentException(
                "Somente relatório mensal usa dia do mês.", nameof(dayOfMonth));
        }

        RecipientsJson = JsonSerializer.Serialize(normalized);
        Frequency = frequency;
        SendTimeUtc = sendTimeUtc;
        DayOfWeek = frequency == ReportFrequency.Weekly ? dayOfWeek : null;
        DayOfMonth = frequency == ReportFrequency.Monthly ? dayOfMonth : null;
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

    /// <summary>
    /// Próximo disparo estritamente após <paramref name="afterUtc"/> (UTC).
    /// SendTimeUtc é o horário de parede de Brasília digitado pelo admin —
    /// converte para o instante UTC real (America/Sao_Paulo).
    /// </summary>
    public DateTime ComputeNextRun(DateTime afterUtc)
    {
        var after = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var afterBr = TimeZoneInfo.ConvertTimeFromUtc(after, SaoPauloTimeZone);

        if (Frequency == ReportFrequency.Daily)
        {
            var candidateBr = afterBr.Date.Add(SendTimeUtc.ToTimeSpan());
            var candidate = SaoPauloToUtc(candidateBr);
            if (candidate <= after)
            {
                candidateBr = candidateBr.AddDays(1);
                candidate = SaoPauloToUtc(candidateBr);
            }
            return candidate;
        }

        if (Frequency == ReportFrequency.Weekly)
        {
            // Próximo DayOfWeek no SendTimeUtc (0–6), dias contados em Brasília.
            for (var i = 0; i < 8; i++)
            {
                var dayBr = afterBr.Date.AddDays(i);
                if ((int)dayBr.DayOfWeek != DayOfWeek)
                    continue;

                var candidate = SaoPauloToUtc(dayBr.Add(SendTimeUtc.ToTimeSpan()));
                if (candidate > after)
                    return candidate;
            }
        }

        if (Frequency == ReportFrequency.Monthly)
        {
            // DayOfMonth no SendTimeUtc de Brasília; mês sem o dia → último dia.
            var year = afterBr.Year;
            var month = afterBr.Month;
            for (var i = 0; i < 14; i++)
            {
                var dayCount = DateTime.DaysInMonth(year, month);
                var day = Math.Min(DayOfMonth ?? 1, dayCount);
                var candidateBr = new DateTime(year, month, day)
                    .Add(SendTimeUtc.ToTimeSpan());
                var candidate = SaoPauloToUtc(candidateBr);
                if (candidate > after)
                    return candidate;

                month++;
                if (month > 12) { month = 1; year++; }
            }
        }

        // Inalcançável (loops cobrem o horizonte), fallback defensivo.
        return after.AddDays(8);
    }

    /// <summary>Fuso fixo da agenda — o horário digitado é sempre de Brasília.</summary>
    private static readonly TimeZoneInfo SaoPauloTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Horário de parede de Brasília → instante UTC.</summary>
    private static DateTime SaoPauloToUtc(DateTime saoPauloWallTime)
        => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(saoPauloWallTime, DateTimeKind.Unspecified),
            SaoPauloTimeZone);

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
