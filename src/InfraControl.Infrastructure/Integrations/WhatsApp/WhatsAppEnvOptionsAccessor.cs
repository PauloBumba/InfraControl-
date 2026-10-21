using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.WhatsApp;

/// <summary>
/// ADR-062: opções WhatsApp de env/config (fallback) expostas para o
/// Application via porta.
/// </summary>
public sealed class WhatsAppEnvOptionsAccessor(
    IOptions<NotificationChannelsOptions> options)
    : IWhatsAppEnvOptionsAccessor
{
    public WhatsAppChannelOptions Current => options.Value.WhatsApp;
}
