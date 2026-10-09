namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// F26 Fase 2.2: cria um <see cref="IUnifiApiClient"/> para opções
/// específicas (config do banco ou env) — o cliente DI continua sendo a
/// instância "padrão"; comandos de teste/sync usam a fábrica para aplicar
/// a configuração efetiva resolvida.
/// </summary>
public interface IUnifiClientFactory
{
    IUnifiApiClient Create(UnifiIntegrationOptions options);
}
