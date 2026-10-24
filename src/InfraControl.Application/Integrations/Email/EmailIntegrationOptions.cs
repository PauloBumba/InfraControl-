namespace InfraControl.Application.Integrations.Email;

/// <summary>
/// F41-T001: opções SMTP efetivas do canal de e-mail. Host vazio =
/// canal não configurado (adaptador de logging). Usado pelo teste de
/// conexão, pelo NotificationEmailWorker e pelo SmtpIdentityEmailSender
/// — todos falam com a MESMA configuração via IEmailOptionsResolver.
/// </summary>
public sealed class EmailIntegrationOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public bool UseSsl { get; set; } = true;

    public bool IsSmtpConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(From);
}
