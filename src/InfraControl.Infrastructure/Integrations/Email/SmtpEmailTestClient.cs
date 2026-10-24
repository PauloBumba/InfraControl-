using InfraControl.Application.Integrations.Email;
using InfraControl.Application.Integrations.Email.Ports;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T001/T002: botão "Testar" da UI — envia um e-mail de prova ao
/// próprio remetente com a configuração efetiva (banco → env). Falha de
/// rede/credencial propaga como exception (o handler captura e grava
/// LastTestError sem segredos).
/// </summary>
public sealed class SmtpEmailTestClient : IEmailTestClient
{
    public async Task SendTestAsync(
        EmailIntegrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var (html, plainText) = EmailTemplate.Build(
            "Teste de e-mail — InfraControl",
            "Este é um e-mail de teste enviado pelo InfraControl. "
            + "Se você o recebeu, o canal SMTP está configurado corretamente.",
            greeting: null);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            options.FromName ?? "InfraControl", options.From));
        mime.To.Add(MailboxAddress.Parse(options.From));
        mime.Subject = "Teste de e-mail — InfraControl";

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
    }
}
