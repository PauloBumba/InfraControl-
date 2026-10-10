namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// F26 Fase 2.2: resolve as opções UniFi EFETIVAS — linha persistida no
/// banco (senha decifrada sob demanda) com fallback para env (F20) quando
/// não existe configuração salva. Usado pelo sync, health e pelo teste de
/// conexão para que todos falem com a MESMA configuração.
/// </summary>
public interface IUniFiOptionsResolver
{
    Task<UnifiIntegrationOptions> ResolveAsync(CancellationToken cancellationToken);
}
