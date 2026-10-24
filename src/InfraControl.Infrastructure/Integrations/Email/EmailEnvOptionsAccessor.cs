using InfraControl.Application.Integrations.Email;
using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.Notifications.Channels;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T001: opções SMTP de env/config (fallback) expostas para o
/// Application via porta — seção Notifications:Channels:Email.
/// </summary>
public sealed class EmailEnvOptionsAccessor(
    IOptions<NotificationChannelsOptions> options)
    : IEmailEnvOptionsAccessor
{
    public EmailIntegrationOptions Current
    {
        get
        {
            var email = options.Value.Email;
            return new EmailIntegrationOptions
            {
                Host = email.Host,
                Port = email.Port,
                Username = email.Username,
                Password = email.Password,
                From = email.From,
                FromName = email.FromName,
                UseSsl = email.UseSsl
            };
        }
    }
}
