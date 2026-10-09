using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Unifi;

/// <summary>
/// F26 Fase 2.2: opções UniFi de env/config (fallback da F20) expostas
/// para o Application via porta.
/// </summary>
public sealed class UnifiEnvOptionsAccessor(IOptions<UnifiIntegrationOptions> options)
    : IUnifiEnvOptionsAccessor
{
    public UnifiIntegrationOptions Current => options.Value;
}
