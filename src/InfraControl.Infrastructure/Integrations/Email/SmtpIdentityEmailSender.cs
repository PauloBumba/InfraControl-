using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.Identity.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T002: transporte real dos e-mails de identidade (convite F1-T017 e
/// forgot-password) via MailKit — mesmo SMTP configurado do canal de
/// notificações (IEmailOptionsResolver). Caminho direto (decisão F1-T017,
/// fora do outbox). Falha de rede/credencial propaga (o handler de
/// convite desfaz a linha; o forgot-password já trata).
/// </summary>
public sealed class SmtpIdentityEmailSender(
    IEmailOptionsResolver optionsResolver,
    ILogger<SmtpIdentityEmailSender> logger)
    : IIdentityEmailSender
{
    public Task SendPasswordResetAsync(
        string email, string resetUrl, CancellationToken cancellationToken = default)
        => SendAsync(
            email,
            "Redefinição de senha — InfraControl",
            "Recebemos um pedido de redefinição de senha para a sua conta. "
            + "Use o botão abaixo para escolher uma nova senha. Se você não "
            + "solicitou este pedido, ignore este e-mail.",
            new EmailTemplate.Cta("Redefinir senha", resetUrl),
            cancellationToken);

    public Task SendInvitationAsync(
        string email, string acceptUrl, CancellationToken cancellationToken = default)
        => SendAsync(
            email,
            "Convite — InfraControl",
            "Você foi convidado a participar do InfraControl. "
            + "Use o botão abaixo para aceitar o convite e criar seu acesso.",
            new EmailTemplate.Cta("Aceitar convite", acceptUrl),
            cancellationToken);

    private async Task SendAsync(
        string email,
        string subject,
        string description,
        EmailTemplate.Cta cta,
        CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsSmtpConfigured)
            throw new InvalidOperationException(
                "Canal de e-mail não configurado — informe host e remetente em Integrações → Email.");

        var (html, plainText) = EmailTemplate.Build(
            subject, description, cta);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            options.FromName ?? "InfraControl", options.From));
        mime.To.Add(MailboxAddress.Parse(email));
        mime.Subject = subject;

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
            "E-mail de identidade '{Subject}' enviado para {To} via {Host}.",
            subject, email, options.Host);
    }
}
