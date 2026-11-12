using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// ADR-059 §4 (F33-T002): dispatcher REAL do canal WhatsApp via Evolution
/// API — substitui <see cref="LoggingWhatsAppNotificationDispatcher"/>
/// condicionalmente (BaseUrl vazio = logging preservado, opt-in). POST em
/// <c>{BaseUrl}/message/sendText/{InstanceId}</c> com header <c>apikey</c> e
/// corpo <c>{ number, text }</c> (contrato real validado no spike F33-T001 —
/// o formato <c>textMessage.text</c> das docs v1 NÃO existe na v2.3.7).
/// Resiliência idêntica ao n8n (retry 3× 1s/2s/4s jitter + CB 5/30s→60s;
/// 4xx sem retry). REGRA (ADR-059 §1/regra 7): só saída de notificação — a
/// Evolution nunca decide estado de Incident/Alert no InfraControl.
/// </summary>
public sealed class EvolutionApiWhatsAppNotificationDispatcher(
    IHttpClientFactory httpClientFactory,
    IWhatsAppOptionsResolver optionsResolver,
    IIncidentImageRenderer imageRenderer,
    ILogger<EvolutionApiWhatsAppNotificationDispatcher> logger) : IWhatsAppNotificationDispatcher
{
    /// <summary>Nome do client (policies Polly registradas na DI).</summary>
    public const string HttpClientName = "evolution-api";

    /// <summary>ADR-059 §4: mesmo spec do dispatcher n8n (F19-T007).</summary>
    public static void ConfigureResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        TimeSpan? retryDelay = null)
        => builder
            .AddRetry(CreateRetryOptions(retryDelay))
            .AddCircuitBreaker(CreateCircuitBreakerOptions());

    /// <summary>Especificação do retry — exposta para teste.</summary>
    public static RetryStrategyOptions<HttpResponseMessage> CreateRetryOptions(
        TimeSpan? retryDelay = null)
        => new()
        {
            MaxRetryAttempts = 3,
            Delay = retryDelay ?? TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = Failures()
        };

    /// <summary>Especificação do circuit breaker — exposta para teste.</summary>
    public static CircuitBreakerStrategyOptions<HttpResponseMessage> CreateCircuitBreakerOptions()
        => new()
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(60),
            ShouldHandle = Failures()
        };

    private static PredicateBuilder<HttpResponseMessage> Failures()
        => new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(static response => (int)response.StatusCode >= 500);

    public async Task SendAsync(
        WhatsAppNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        // ADR-062: options efetivas por request (banco → env) — o Grupo
        // escolhido na UI (banco) é refletido sem restart.
        var whatsapp = await optionsResolver.ResolveAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(whatsapp.BaseUrl))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:BaseUrl configurado (Notifications:Channels:WhatsApp:BaseUrl).");

        if (string.IsNullOrWhiteSpace(whatsapp.InstanceId))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:InstanceId configurado (Notifications:Channels:WhatsApp:InstanceId).");

        if (string.IsNullOrWhiteSpace(whatsapp.ApiKey))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:ApiKey configurado (Notifications:Channels:WhatsApp:ApiKey).");

        // ADR-059 §3: GroupId (grupo) é prioritário sobre Phone (número).
        var number = whatsapp.GroupId;
        if (string.IsNullOrWhiteSpace(number))
            number = message.Phone;

        if (string.IsNullOrWhiteSpace(number))
            throw new InvalidOperationException(
                "Canal WhatsApp sem destino (Notifications:Channels:WhatsApp:GroupId ou Phone).");

        var client = httpClientFactory.CreateClient(HttpClientName);
        var text = BuildText(message);

        // F33-T005/ADR-059 decisão 7: com gráfico, tenta sendMedia com o
        // PNG (caption = mesmo texto); geração ou envio da mídia falhou →
        // log Warning e o evento sai em TEXTO — nunca fica por enviar por
        // causa do gráfico. Polly cobre as duas chamadas (handler do client).
        if (message.Chart is not null)
        {
            try
            {
                var png = imageRenderer.RenderPng(message.Chart);
                await SendMediaAsync(
                    client, whatsapp, number, png, text,
                    message.DeliveryId, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "WhatsApp {DeliveryId}: falha no envio do gráfico — fallback para sendText.",
                    message.DeliveryId);
            }
        }

        await SendTextAsync(client, whatsapp, number, text, message, cancellationToken);
    }

    private async Task SendTextAsync(
        HttpClient client,
        WhatsAppChannelOptions whatsapp,
        string number,
        string text,
        WhatsAppNotificationMessage message,
        CancellationToken cancellationToken)
    {
        var url = $"{whatsapp.BaseUrl!.TrimEnd('/')}/message/sendText/{whatsapp.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new EvolutionSendText(number, text))
        };
        request.Headers.TryAddWithoutValidation("apikey", whatsapp.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);

        // Resposta final não-2xx (após o retry do handler) → exceção → o
        // worker marca a delivery Failed com o erro pt-BR.
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp {DeliveryId} enviado via Evolution para {Number} (usuário {UserId}): HTTP {StatusCode}.",
            message.DeliveryId, number, message.UserId, (int)response.StatusCode);
    }

    /// <summary>
    /// F33-T005: contrato do sendMedia na v2.3.7 — <c>{ number, mediatype,
    /// mimetype, media (base64), caption, fileName }</c> (rota viva
    /// confirmada no spike F33-T001; corpo sondado no spike da T005).
    /// </summary>
    private async Task SendMediaAsync(
        HttpClient client,
        WhatsAppChannelOptions whatsapp,
        string number,
        byte[] png,
        string caption,
        Guid messageDeliveryId,
        CancellationToken cancellationToken)
    {
        var url = $"{whatsapp.BaseUrl!.TrimEnd('/')}/message/sendMedia/{whatsapp.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new EvolutionSendMedia(
                number,
                Mediatype: "image",
                Mimetype: "image/png",
                Media: Convert.ToBase64String(png),
                Caption: caption,
                FileName: "infracontrol-evento.png"))
        };
        request.Headers.TryAddWithoutValidation("apikey", whatsapp.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp {DeliveryId} enviado via Evolution (sendMedia, {Bytes} bytes PNG) para {Number}: HTTP {StatusCode}.",
            messageDeliveryId, png.Length, number, (int)response.StatusCode);
    }

    /// <summary>
    /// Texto do WhatsApp em PT-BR padronizado (seções + emojis): cabeçalho
    /// com o tipo do evento, linha do equipamento/problema (deduplicada —
    /// a media type 105 manda "{HOST}: {TRIGGER}" e triggers de telefonia já
    /// repetem o nome do host), severidade, dica contextual e data. Com o
    /// gráfico habilitado (F33-T005) este mesmo texto vira a caption do
    /// sendMedia.
    /// </summary>
    private static string BuildText(WhatsAppNotificationMessage message)
    {
        var kind = Classify(message.Title);
        var subject = ExtractSubject(message.Message, message.Title);
        var severity = ParseSeverity(message.Message, message.Title);
        var stamp = message.CreatedAt.ToString("dd/MM/yyyy HH:mm");

        var lines = new List<string>();

        if (kind == EventKind.Resolved)
        {
            lines.Add($"🟢 *{ResolvedHeader(message.Title)}*");
            lines.Add($"📍 {subject}");
        }
        else if (kind == EventKind.Generic)
        {
            lines.Add($"🔔 *{message.Title}*");
            lines.Add(subject);
        }
        else
        {
            var emoji = SeverityEmoji(severity);
            lines.Add($"{emoji} *{ProblemHeader(kind, severity)}*");
            lines.Add($"📍 {subject}");
            if (!string.IsNullOrWhiteSpace(severity))
                lines.Add($"🎚️ Severidade: {severity}");
            if (subject.Contains("fora do ar", StringComparison.OrdinalIgnoreCase))
                lines.Add("💡 Verificar o equipamento e a rede local.");
        }

        lines.Add($"🕐 {stamp}");
        return string.Join("\n", lines);
    }

    private enum EventKind { AlertCritical, Alert, Incident, Resolved, Generic }

    private static EventKind Classify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return EventKind.Generic;

        var upper = title.ToUpperInvariant();
        if (upper.Contains("RESOLVID"))
            return EventKind.Resolved;
        if (upper.Contains("INCIDENT"))
            return EventKind.Incident;
        if (upper.Contains("CRÍTICO") || upper.Contains("CRITICO"))
            return EventKind.AlertCritical;
        if (upper.Contains("ALERTA") || upper.Contains("ALERT"))
            return EventKind.Alert;
        return EventKind.Generic;
    }

    private static string ProblemHeader(EventKind kind, string severity)
    {
        var label = kind switch
        {
            EventKind.Incident => "Incidente",
            EventKind.AlertCritical => "Alerta crítico",
            _ => "Alerta"
        };
        return string.IsNullOrWhiteSpace(severity) || label.Contains(severity, StringComparison.OrdinalIgnoreCase)
            ? label
            : $"{label} — {severity}";
    }

    private static string ResolvedHeader(string title)
        => title.Trim().ToLowerInvariant() switch
        {
            var t when t.Contains("incidente") => "Incidente resolvido",
            _ => "Alerta resolvido"
        };

    private static string SeverityEmoji(string severity) => severity switch
    {
        "CRÍTICO" => "🔴",
        "ALTO" => "🟠",
        "MÉDIO" => "🟡",
        "BAIXO" => "🔵",
        _ => "⚪"
    };

    /// <summary>
    /// Extrai o assunto entre aspas simples das mensagens do consumer
    /// ("O alerta 'X' foi aberto (SEV).") e remove a duplicação
    /// "{HOST}: {HOST} resto" gerada pela media type do Zabbix — quando a
    /// parte após os dois-pontos começa repetindo o prefixo, fica só o resto.
    /// </summary>
    private static string ExtractSubject(string? message, string fallback)
    {
        var text = message?.Trim() ?? string.Empty;
        var first = text.IndexOf('\'');
        var last = first < 0 ? -1 : text.IndexOf('\'', first + 1);
        if (first >= 0 && last > first)
            text = text[(first + 1)..last];

        if (string.IsNullOrWhiteSpace(text))
            text = CleanMessage(message) is { Length: > 0 } cleaned ? cleaned : fallback;

        return DedupHostPrefix(text.Trim());
    }

    private static string DedupHostPrefix(string subject)
    {
        var idx = subject.IndexOf(": ", StringComparison.Ordinal);
        if (idx <= 0)
            return subject;

        var prefix = subject[..idx];
        var rest = subject[(idx + 2)..];
        return rest.StartsWith(prefix + " ", StringComparison.Ordinal)
            ? rest
            : subject;
    }

    /// <summary>
    /// Severidade em pt-BR a partir do sufixo da mensagem
    /// ("(CRÍTICO)", "(High)"…) com fallback no título ("Alerta crítico").
    /// </summary>
    private static string ParseSeverity(string? message, string title)
    {
        var text = $"{message} {title}".ToUpperInvariant();
        if (text.Contains("CRÍTICO") || text.Contains("CRITICO") || text.Contains("CRITICAL") || text.Contains("DISASTER"))
            return "CRÍTICO";
        if (text.Contains("ALTO") || text.Contains("HIGH"))
            return "ALTO";
        if (text.Contains("MÉDIO") || text.Contains("MEDIO") || text.Contains("MEDIUM"))
            return "MÉDIO";
        if (text.Contains("BAIXO") || text.Contains("LOW"))
            return "BAIXO";
        if (text.Contains("AVISO") || text.Contains("WARNING"))
            return "AVISO";
        return string.Empty;
    }

    private static string CleanMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message
            .Replace("O incidente '", string.Empty)
            .Replace("O alerta '", string.Empty)
            .Replace("' foi aberto", string.Empty)
            .Replace("' foi resolvido", string.Empty)
            .Replace(" foi aberto", string.Empty)
            .Replace(" foi resolvido", string.Empty)
            .Replace(" (CRÍTICO)", string.Empty)
            .Replace(" (ALTO)", string.Empty)
            .Replace(" (MÉDIO)", string.Empty)
            .Replace(" (BAIXO)", string.Empty)
            .Replace(" (AVISO)", string.Empty)
            .Replace(" (Critical)", string.Empty)
            .Replace(" (High)", string.Empty)
            .Replace(" (Medium)", string.Empty)
            .Replace(" (Low)", string.Empty)
            .Replace(" (Warning)", string.Empty)
            .Trim(' ', '.', '\'');
    }

    /// <summary>Corpo do POST — contrato real da v2.3.7 validado no spike.</summary>
    private sealed record EvolutionSendText(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("text")] string Text);

    /// <summary>Corpo do sendMedia (spike F33-T005).</summary>
    private sealed record EvolutionSendMedia(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("mediatype")] string Mediatype,
        [property: JsonPropertyName("mimetype")] string Mimetype,
        [property: JsonPropertyName("media")] string Media,
        [property: JsonPropertyName("caption")] string Caption,
        [property: JsonPropertyName("fileName")] string FileName);
}
