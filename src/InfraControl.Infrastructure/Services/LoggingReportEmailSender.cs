using InfraControl.Application.Integrations.Email.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Adaptador de log do remetente de relatórios — mesmo padrão do
/// LoggingIdentityEmailSender: sem SMTP configurado o e-mail é apenas
/// registrado (dev/teste); com SMTP o SmtpReportEmailSender envia de
/// verdade via factory no DI.
/// </summary>
public sealed class LoggingReportEmailSender(
    ILogger<LoggingReportEmailSender> logger)
    : IReportEmailSender
{
    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Relatório por e-mail para {To}: {Subject} ({TextLength} chars texto)",
            to, subject, textBody.Length);

        return Task.CompletedTask;
    }
}
