namespace InfraControl.Application.Integrations.Email.Ports;

/// <summary>
/// Envio direto de e-mail transacional (relatório agendado e futuros
/// envios administrativos) — mesmo SMTP do IEmailOptionsResolver,
/// fora do pipeline de notificações (não é NotificationDelivery).
/// </summary>
public interface IReportEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
