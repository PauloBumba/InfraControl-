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
    public WhatsAppChannelOptions Current
    {
        get
        {
            var channels = options.Value;
            var whatsapp = channels.WhatsApp;
            return new WhatsAppChannelOptions
            {
                Phone = whatsapp.Phone,
                GroupId = whatsapp.GroupId,
                BaseUrl = whatsapp.BaseUrl,
                ApiKey = whatsapp.ApiKey,
                InstanceId = whatsapp.InstanceId,
                NotificationsEnabled = channels.Default.Contains(
                    "WhatsApp", StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
