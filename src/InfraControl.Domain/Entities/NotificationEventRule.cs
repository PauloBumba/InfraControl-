using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F42-T001: regra de notificação por tipo de evento (granularidade que
/// faltava — hoje o fanout é lista global para todos os 13 eventos).
/// Uma linha por EventType (índice único). Sem linha = comportamento
/// atual (fanout global, zero regressão). Com linha:
/// <list type="bullet">
/// <item><see cref="Enabled"/> = false → o evento NÃO gera notificação;</item>
/// <item><see cref="Channels"/> preenchido → canais da regra substituem o
/// fanout global para ESTE evento;</item>
/// <item><see cref="Channels"/> vazio → canais do fanout global
/// (Enabled=true apenas confirma que o evento está ligado).</item>
/// </list>
/// Canais são nomes do enum <c>NotificationChannel</c> separados por
/// vírgula (ex.: "Email,SignalR").
/// </summary>
public sealed class NotificationEventRule : Entity
{
    public const int MaxEventTypeLength = 255;
    public const int MaxChannelsLength = 500;

    public string EventType { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public string Channels { get; private set; } = string.Empty;

    private NotificationEventRule() { }

    public static NotificationEventRule Create(
        string eventType, bool enabled, string channels)
    {
        var rule = new NotificationEventRule();
        rule.Update(eventType, enabled, channels);
        return rule;
    }

    public void Update(string eventType, bool enabled, string channels)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException(
                "O tipo de evento é obrigatório.", nameof(eventType));

        if (eventType.Length > MaxEventTypeLength)
            throw new ArgumentException(
                $"O tipo de evento pode ter no máximo {MaxEventTypeLength} caracteres.",
                nameof(eventType));

        if (channels is { Length: > MaxChannelsLength })
            throw new ArgumentException(
                $"A lista de canais pode ter no máximo {MaxChannelsLength} caracteres.",
                nameof(channels));

        EventType = eventType.Trim();
        Enabled = enabled;
        Channels = channels?.Trim() ?? string.Empty;
        Touch();
    }

    /// <summary>Canais parseados (nomes do enum), na ordem configurada.</summary>
    public IReadOnlyList<string> ChannelNames =>
        string.IsNullOrWhiteSpace(Channels)
            ? Array.Empty<string>()
            : Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
