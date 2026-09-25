using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Envelope JSON do outbox (docs/06) — serialização compartilhada entre o
/// writer (DomainEventService) e o reader (OutboxDispatcherWorker);
/// enums como string para payload legível (ex.: "Role":"Admin").
/// </summary>
public static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
