using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Unifi;

/// <summary>
/// F26 Fase 2.2: cria um <see cref="UnifiApiClient"/> para opções
/// específicas (config persistida ou env) — reaproveita o named client
/// <c>unifi</c> (Polly + handler TLS) do IHttpClientFactory.
/// </summary>
public sealed class UnifiClientFactory(
    IHttpClientFactory httpClientFactory,
    ILogger<UnifiApiClient> logger)
    : IUnifiClientFactory
{
    public IUnifiApiClient Create(UnifiIntegrationOptions options)
        => new UnifiApiClient(
            httpClientFactory,
            Options.Create(options),
            logger);
}
