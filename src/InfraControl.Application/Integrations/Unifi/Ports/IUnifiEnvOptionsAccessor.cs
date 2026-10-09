namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// F26 Fase 2.2: opções UniFi vindas de env/config (fallback da F20 quando
/// não existe linha no banco). Implementação em Infrastructure envolve
/// <c>IOptions&lt;UnifiIntegrationOptions&gt;</c> para o Application não
/// depender de Options.
/// </summary>
public interface IUnifiEnvOptionsAccessor
{
    UnifiIntegrationOptions Current { get; }
}
