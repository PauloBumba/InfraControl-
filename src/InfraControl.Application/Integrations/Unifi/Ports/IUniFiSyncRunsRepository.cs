using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>F26 Fase 9.1: execuções de sincronização persistidas.</summary>
public interface IUniFiSyncRunsRepository : IRepository<UniFiSyncRun>
{
    Task<IReadOnlyList<UniFiSyncRun>> ListRecentAsync(
        int limit, bool? succeeded, DateTime? fromUtc, DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
