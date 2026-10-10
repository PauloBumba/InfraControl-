namespace InfraControl.Application.Integrations.Zabbix.Ports;

/// <summary>
/// F27 Fase 2.2: resolve as opções Zabbix EFETIVAS — linha persistida no
/// banco (senha decifrada sob demanda) com fallback para env quando não
/// existe configuração salva. Usado pelo teste de conexão e pelo cliente
/// da API para que todos falem com a MESMA configuração.
/// </summary>
public interface IZabbixOptionsResolver
{
    Task<ZabbixIntegrationOptions> ResolveAsync(CancellationToken cancellationToken);
}
