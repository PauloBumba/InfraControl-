using System.Text.Json;
using System.Text.RegularExpressions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Messaging.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Explorer;

/// <summary>
/// F55-T004: Message Explorer — outbox/inbox/deliveries reais do banco.
/// Payload SEMPRE sanitizado antes de sair da API (tokens, senhas,
/// credenciais e chaves sensíveis viram [REDACTED]).
/// </summary>
public static class MessagingPayloadSanitizer
{
    private const int MaxPayloadChars = 8000;

    private static readonly Regex SensitiveKey = new(
        "pass|secret|token|apikey|api_key|authorization|credential|cookie|private|pwd|senha|chave",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Sanitize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var sanitized = SanitizeElement(doc.RootElement);
            var json = JsonSerializer.Serialize(sanitized);
            return json.Length > MaxPayloadChars
                ? json[..MaxPayloadChars] + "…[truncado]"
                : json;
        }
        catch (JsonException)
        {
            // Payload não-JSON (raro): mascara padrões chave=valor óbvios.
            var masked = Regex.Replace(payload,
                @"(""(?:pass|secret|token|api_?key|authorization|credential|cookie|pwd|senha|chave)[^""]*""\s*:\s*)""[^""]*""",
                "$1\"[REDACTED]\"",
                RegexOptions.IgnoreCase);
            return masked.Length > MaxPayloadChars
                ? masked[..MaxPayloadChars] + "…[truncado]"
                : masked;
        }
    }

    private static object? SanitizeElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var map = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    map[prop.Name] = SensitiveKey.IsMatch(prop.Name)
                        ? "[REDACTED]"
                        : SanitizeElement(prop.Value);
                }
                return map;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(SanitizeElement).ToArray();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.TryGetInt64(out var l) ? l : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }
}

public sealed record MessagingMessageItemDto(
    Guid Id,
    string EventType,
    string? CorrelationId,
    DateTime OccurredAt,
    DateTime? ProcessedAt,
    string? Error,
    int RetryCount,
    int MaxRetries,
    string State,
    string? PayloadPreview);

public sealed record MessagingMessageTimelineEntry(
    string Stage,
    DateTime? AtUtc,
    string? Detail);

public sealed record MessagingMessageDetailDto(
    Guid Id,
    string EventType,
    string? CorrelationId,
    DateTime OccurredAt,
    DateTime? ProcessedAt,
    double? ProcessingDurationSeconds,
    string? Error,
    int RetryCount,
    int MaxRetries,
    string State,
    string SanitizedPayload,
    IReadOnlyList<MessagingMessageTimelineEntry> Timeline,
    IReadOnlyList<InboxRecordDto> InboxRecords);

public sealed record InboxRecordDto(
    Guid EventId,
    string Consumer,
    DateTime ProcessedAt);

public sealed record SearchMessagingMessagesQuery(
    Guid? MessageId, string? CorrelationId, string? EventType, string? State,
    string? Text, int? MinAttempts, DateTime? FromUtc, DateTime? ToUtc,
    int Page, int PageSize)
    : BaseQuery<PagedResult<MessagingMessageItemDto>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SearchMessagingMessagesQueryHandler(IMessagingExplorerStore store)
    : IRequestHandler<SearchMessagingMessagesQuery, PagedResult<MessagingMessageItemDto>>
{
    public async Task<PagedResult<MessagingMessageItemDto>> Handle(
        SearchMessagingMessagesQuery request, CancellationToken cancellationToken)
    {
        var result = await store.SearchOutboxAsync(new MessagingMessageFilter(
            request.MessageId, request.CorrelationId, request.EventType,
            request.State, request.Text, request.MinAttempts,
            request.FromUtc, request.ToUtc, request.Page, request.PageSize),
            cancellationToken);

        return new PagedResult<MessagingMessageItemDto>(
            result.Items.Select(MapItem).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }

    private static MessagingMessageItemDto MapItem(OutboxMessage m) => new(
        m.Id, m.EventType, m.CorrelationId, m.OccurredAt, m.ProcessedAt,
        m.Error, m.RetryCount, m.MaxRetries,
        State: m.ProcessedAt != null ? "processed"
            : m.RetryCount >= m.MaxRetries ? "exhausted"
            : m.Error is not null ? "failed" : "pending",
        PayloadPreview: Preview(m.Payload));

    private static string? Preview(string payload)
    {
        var clean = payload.Length > 160 ? payload[..160] + "…" : payload;
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }
}

public sealed record GetMessagingMessageDetailQuery(Guid Id)
    : BaseQuery<MessagingMessageDetailDto?>, IAuthorizedRequest
{
    public override string CacheKey => $"{GetType().Name}:{Id}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingMessageDetailQueryHandler(IMessagingExplorerStore store)
    : IRequestHandler<GetMessagingMessageDetailQuery, MessagingMessageDetailDto?>
{
    public async Task<MessagingMessageDetailDto?> Handle(
        GetMessagingMessageDetailQuery request, CancellationToken cancellationToken)
    {
        var message = await store.GetOutboxAsync(request.Id, cancellationToken);
        if (message is null) return null;

        var inbox = await store.GetInboxByEventIdAsync(request.Id, cancellationToken);

        var timeline = new List<MessagingMessageTimelineEntry>
        {
            new("Publicada (outbox)", message.OccurredAt, message.EventType),
        };
        if (message.RetryCount > 0 || message.Error is not null)
        {
            timeline.Add(new MessagingMessageTimelineEntry(
                "Falhas de processamento",
                message.ProcessedAt ?? message.UpdateDateTime,
                $"{message.RetryCount} tentativa(s) · {message.Error}"));
        }
        if (message.ProcessedAt is { } processedAt)
        {
            timeline.Add(new MessagingMessageTimelineEntry(
                "Processada", processedAt,
                $"duração {(processedAt - message.OccurredAt).TotalSeconds:F2}s"));
        }
        foreach (var record in inbox)
        {
            timeline.Add(new MessagingMessageTimelineEntry(
                $"Inbox: {record.Consumer}", record.ProcessedAt, "deduplicada/processada"));
        }

        return new MessagingMessageDetailDto(
            message.Id, message.EventType, message.CorrelationId,
            message.OccurredAt, message.ProcessedAt,
            message.ProcessedAt is { } p
                ? (p - message.OccurredAt).TotalSeconds : null,
            message.Error, message.RetryCount, message.MaxRetries,
            State: message.ProcessedAt != null ? "processed"
                : message.RetryCount >= message.MaxRetries ? "exhausted"
                : message.Error is not null ? "failed" : "pending",
            SanitizedPayload: MessagingPayloadSanitizer.Sanitize(message.Payload),
            Timeline: timeline,
            InboxRecords: inbox.Select(i => new InboxRecordDto(
                i.EventId, i.Consumer, i.ProcessedAt)).ToArray());
    }
}

public sealed record MessagingDeliveryItemDto(
    Guid Id,
    Guid NotificationId,
    Guid UserId,
    string Channel,
    string Status,
    int AttemptCount,
    DateTime? NextAttemptAt,
    DateTime? SentAt,
    string? LastError,
    string? SuppressionReason,
    DateTime CreatedAt);

public sealed record SearchMessagingDeliveriesQuery(
    Guid? DeliveryId, Guid? NotificationId, string? Channel, string? Status,
    int? MinAttempts, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize)
    : BaseQuery<PagedResult<MessagingDeliveryItemDto>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SearchMessagingDeliveriesQueryHandler(IMessagingExplorerStore store)
    : IRequestHandler<SearchMessagingDeliveriesQuery, PagedResult<MessagingDeliveryItemDto>>
{
    public async Task<PagedResult<MessagingDeliveryItemDto>> Handle(
        SearchMessagingDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var result = await store.SearchDeliveriesAsync(new MessagingDeliveryFilter(
            request.DeliveryId, request.NotificationId, request.Channel,
            request.Status, request.MinAttempts, request.FromUtc, request.ToUtc,
            request.Page, request.PageSize), cancellationToken);

        return new PagedResult<MessagingDeliveryItemDto>(
            result.Items.Select(d => new MessagingDeliveryItemDto(
                d.Id, d.NotificationId, d.UserId, d.Channel.ToString(),
                d.Status.ToString(), d.AttemptCount, d.NextAttemptAt, d.SentAt,
                d.LastError, d.SuppressionReason, d.CreateDateTime)).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }
}
