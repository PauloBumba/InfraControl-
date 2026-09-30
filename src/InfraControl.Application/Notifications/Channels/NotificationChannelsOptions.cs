namespace InfraControl.Application.Notifications.Channels;

/// <summary>
/// F9-T006 (decisão do usuário): o ADMIN escolhe por onde as notificações
/// são enviadas — seção <c>Notifications:Channels</c> no appsettings.
/// <see cref="Default"/> é a lista de canais do fanout do consumer
/// (default Email + SignalR, o comportamento que antes era fixo em código);
/// cada canal de destino externo tem sua seção com o endereço vazio =
/// não configurado (canal na lista sem destino = Warning, sem delivery).
/// Fallback de e-mail: se NENHUM canal da lista for enviável, o consumer
/// enfileira Email — notificação não morre por config incompleta.
/// </summary>
public sealed class NotificationChannelsOptions
{
    public const string SectionName = "Notifications:Channels";

    /// <summary>Canais do fanout, na ordem. Valores = nomes do enum <c>NotificationChannel</c>.</summary>
    public List<string> Default { get; set; } = ["Email", "SignalR"];

    public TelegramChannelOptions Telegram { get; set; } = new();

    public WhatsAppChannelOptions WhatsApp { get; set; } = new();

    public WebhookChannelOptions Webhook { get; set; } = new();

    public SlackChannelOptions Slack { get; set; } = new();
}

/// <summary>Destino global do canal Telegram (chat id do bot).</summary>
public sealed class TelegramChannelOptions
{
    public string ChatId { get; set; } = string.Empty;
}

/// <summary>Destino global do canal WhatsApp (número que recebe as mensagens).</summary>
public sealed class WhatsAppChannelOptions
{
    public string Phone { get; set; } = string.Empty;
}

/// <summary>Destino global do canal Webhook (URL que recebe o POST).</summary>
public sealed class WebhookChannelOptions
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>Destino global do canal Slack (canal que recebe as mensagens).</summary>
public sealed class SlackChannelOptions
{
    public string Channel { get; set; } = string.Empty;
}
