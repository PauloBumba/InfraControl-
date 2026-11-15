using InfraControl.Application.Integrations.Email.Ports;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// Envio SMTP direto de e-mails transacionais (relatório agendado) via
/// MailKit — mesmo IEmailOptionsResolver do identity sender e do canal
/// de notificações, fora do pipeline NotificationDelivery.
/// </summary>
public sealed class SmtpReportEmailSender(
    IEmailOptionsResolver optionsResolver,
    ILogger<SmtpReportEmailSender> logger)
    : IReportEmailSender
{
    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsSmtpConfigured)
            throw new InvalidOperationException(
                "Canal de e-mail não configurado — informe host e remetente em Integrações → Email.");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            options.FromName ?? "InfraControl", options.From));
        mime.To.Add(MailboxAddress.Parse(to));
        mime.Subject = subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        }.ToMessageBody();

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
            "E-mail '{Subject}' enviado para {To} via {Host}.",
            subject, to, options.Host);
    }
}
