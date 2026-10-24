namespace InfraControl.Application.Integrations.Email.Ports;

/// <summary>
/// F41-T001: resolve as opções SMTP EFETIVAS — linha persistida no banco
/// (senha decifrada sob demanda) com fallback para env quando não existe
/// configuração salva. Usado pelo teste, pelo worker do canal Email e pelo
/// identity sender para que todos falem com a MESMA configuração.
/// </summary>
public interface IEmailOptionsResolver
{
    Task<EmailIntegrationOptions> ResolveAsync(CancellationToken cancellationToken);
}
