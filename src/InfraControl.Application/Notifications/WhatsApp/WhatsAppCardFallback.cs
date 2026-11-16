using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InfraControl.Application.Notifications.WhatsApp;

/// <summary>
/// Cartão de fallback (pedido do usuário: "tudo que vai pelo WhatsApp tem
/// formato de card"): quando o <see cref="IWhatsAppChartFactory"/> não
/// consegue série Zabbix (host/item sem correspondência, alerta interno
/// de container, notificação fora do monitoramento), a delivery ainda
/// sai como PNG-cartão com selo, dispositivo, título e o quadro
/// "Sem dados históricos disponíveis" — nunca texto solto improvisado.
/// </summary>
public static class WhatsAppCardFallback
{
    private static readonly Regex QuotedSubject = new(
        "'([^']{3,300})'", RegexOptions.Compiled);

    /// <summary>
    /// Monta o <see cref="IncidentChartData"/> sem série a partir do que a
    /// notification já carrega (título/mensagem do evento de domínio).
    /// </summary>
    public static IncidentChartData Build(
        string title,
        string message,
        string eventType,
        DateTime eventAtUtc,
        string? deviceLabel = null,
        string? hostLabel = null)
    {
        var subject = ExtractSubject(title, message);
        var severity = SeverityFromText($"{subject} {title} {message}");
        return new IncidentChartData(
            KindFor(eventType, severity),
            subject,
            deviceLabel,
            severity,
            eventAtUtc,
            Series: [],
            HostLabel: hostLabel);
    }

    /// <summary>
    /// O assunto real está entre aspas na mensagem dos eventos de domínio
    /// ("O alerta 'X' foi aberto…"). Sem aspas, usa a primeira linha da
    /// mensagem; por fim, o próprio título da notification.
    /// </summary>
    public static string ExtractSubject(string title, string message)
    {
        var quoted = QuotedSubject.Match(message ?? string.Empty);
        if (quoted.Success)
            return quoted.Groups[1].Value.Trim();

        var firstLine = (message ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length <= 160)
            return firstLine;

        return string.IsNullOrWhiteSpace(title) ? "Notificação InfraControl" : title.Trim();
    }

    private static ChartCardKind KindFor(string eventType, string severity)
        => eventType.Contains("Resolved", StringComparison.OrdinalIgnoreCase)
            ? ChartCardKind.Resolution
            : severity is "Warning" or "Medium" or "Low"
                ? ChartCardKind.Warning
                : ChartCardKind.Incident;

    private static string SeverityFromText(string text)
    {
        // Mensagens de domínio usam tanto o nome do enum ("Critical",
        // "High") quanto o rótulo pt-BR ("CRÍTICO", "ALTO").
        var normalized = text.Normalize(NormalizationForm.FormD);
        var plain = new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            .ToArray());

        if (plain.Contains("CRITICO", StringComparison.OrdinalIgnoreCase))
            return "Critical";
        if (plain.Contains("HIGH", StringComparison.OrdinalIgnoreCase)
            || plain.Contains("ALTO", StringComparison.OrdinalIgnoreCase))
            return "High";
        if (plain.Contains("MEDIUM", StringComparison.OrdinalIgnoreCase)
            || plain.Contains("MEDIO", StringComparison.OrdinalIgnoreCase))
            return "Medium";
        if (plain.Contains("WARNING", StringComparison.OrdinalIgnoreCase)
            || plain.Contains("AVISO", StringComparison.OrdinalIgnoreCase))
            return "Warning";
        if (plain.Contains("LOW", StringComparison.OrdinalIgnoreCase)
            || plain.Contains("BAIXO", StringComparison.OrdinalIgnoreCase))
            return "Low";
        if (plain.Contains("OK", StringComparison.OrdinalIgnoreCase))
            return "OK";

        return "—";
    }
}
