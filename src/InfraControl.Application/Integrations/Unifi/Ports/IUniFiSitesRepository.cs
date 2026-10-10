using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// F26 Fase 3.1: sites UniFi persistidos (cadastro — REGRA 8: sem
/// telemetria; site que some do controller vira `MissingSinceUtc`, nunca
/// é apagado — histórico preservado).
/// </summary>
public interface IUniFiSitesRepository
{
    Task<IReadOnlyList<UniFiSite>> ListAsync(CancellationToken cancellationToken);

    Task<UniFiSite?> GetByShortIdAsync(string shortId, CancellationToken cancellationToken);

    Task AddAsync(UniFiSite site, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
