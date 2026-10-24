using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.Notifications.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T002: transporte real do canal Email via MailKit. Resolve as
/// options EFETIVAS (banco → env) a cada envio — troca na UI vale sem
/// restart, mesmo contrato do dispatcher WhatsApp. Falha de
/// rede/credencial propaga para o worker aplicar retry/DLQ.
/// </summary>
public sealed class SmtpEmailNotificationDispatcher(
    IEmailOptionsResolver optionsResolver,
    ILogger<SmtpEmailNotificationDispatcher> logger)
    : IEmailNotificationDispatcher
{
    public async Task SendAsync(
        EmailNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsSmtpConfigured)
            throw new InvalidOperationException(
                "Canal de e-mail não configurado — informe host e remetente em Integrações → Email.");

        if (string.IsNullOrWhiteSpace(message.To))
            throw new InvalidOperationException(
                "Destinatário sem e-mail — cadastre o e-mail do usuário antes de enviar notificações.");

        var (html, plainText) = EmailTemplate.Build(
            message.Subject,
            message.Body,
            greeting: null);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            options.FromName ?? "InfraControl", options.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = html,
            TextBody = plainText
        };
        mime.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            options.Host, options.Port,
            options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
            cancellationToken);

        if (!string.IsNullOrEmpty(options.Username))
            await client.AuthenticateAsync(
                options.Username, options.Password, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation(
            "E-mail de notificação {DeliveryId} enviado para {To} via {Host}.",
            message.DeliveryId, message.To, options.Host);
    }
}
