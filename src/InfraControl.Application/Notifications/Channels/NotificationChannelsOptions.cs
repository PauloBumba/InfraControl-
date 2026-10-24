namespace InfraControl.Application.Notifications.Channels;

/// <summary>
/// F9-T006 (decisão do usuário): o ADMIN escolhe por onde as notificações
/// são enviadas — seção <c>Notifications:Channels</c> no appsettings.
/// <see cref="Default"/> é a lista de canais do fanout do consumer
/// (default Email + SignalR, o comportamento que antes era fixo em código);
/// cada canal de destino externo tem sua seção com o endereço vazio =
/// não configurado (canal na lista sem destino = Warning, sem delivery).
/// A ativação explícita do WhatsApp na configuração persistida pode
/// acrescentar/remover apenas esse canal sem alterar os demais.
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

    /// <summary>F41-T001: fallback de env do canal Email (banco → env).</summary>
    public EmailChannelOptions Email { get; set; } = new();
}

/// <summary>Destino global do canal Telegram (chat id do bot).</summary>
public sealed class TelegramChannelOptions
{
    public string ChatId { get; set; } = string.Empty;
}

/// <summary>
/// Destino global do canal WhatsApp (ADR-059 §3): <see cref="Phone"/> é o
/// número individual (F9-T007, retrocompatível); <see cref="GroupId"/>
/// (formato <c>&lt;id&gt;@g.us</c>) é o destino GRUPO e é **prioritário**
/// — destino único por canal (ADR-059 §1: proibido destino por usuário).
/// <see cref="BaseUrl"/> vazio = canal fica no adaptador de logging (opt-in).
/// </summary>
public sealed class WhatsAppChannelOptions
{
    /// <summary>
    /// Null follows the legacy Notifications:Channels:Default setting;
    /// an explicit value from the platform overrides it.
    /// </summary>
    public bool? NotificationsEnabled { get; set; }

    public string Phone { get; set; } = string.Empty;

    /// <summary>JID do grupo (&lt;id&gt;@g.us) — prioritário sobre Phone.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>URL da Evolution API consumida pelo dispatcher (vazio = logging).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Header <c>apikey</c> da Evolution — nunca em log/DTO/exceção.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Nome da instância WhatsApp criada na Evolution.</summary>
    public string InstanceId { get; set; } = string.Empty;
}

/// <summary>Destino global do canal Webhook (URL que recebe o POST).</summary>
public sealed class WebhookChannelOptions
{
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// F19-T007/ADR-028 §2: token de assinatura enviado no header
    /// <c>X-InfraControl-Secret</c> (vazio = sem header).
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}

/// <summary>Destino global do canal Slack (canal que recebe as mensagens).</summary>
public sealed class SlackChannelOptions
{
    public string Channel { get; set; } = string.Empty;
}

/// <summary>
/// F41-T001: opções SMTP de env do canal Email (fallback quando não
/// existe linha no banco). Host vazio = canal no adaptador de logging.
/// </summary>
public sealed class EmailChannelOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public bool UseSsl { get; set; } = true;
}
