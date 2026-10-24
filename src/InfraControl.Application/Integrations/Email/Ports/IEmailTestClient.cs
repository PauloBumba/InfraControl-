namespace InfraControl.Application.Integrations.Email.Ports;

/// <summary>
/// F41-T001: cliente SMTP mínimo para o botão "Testar" da UI — envia um
/// e-mail de prova com a configuração efetiva. Implementação real com
/// MailKit em Infrastructure (F41-T002); Application não depende de
/// transporte.
/// </summary>
public interface IEmailTestClient
{
    Task SendTestAsync(
        EmailIntegrationOptions options,
        CancellationToken cancellationToken = default);
}
