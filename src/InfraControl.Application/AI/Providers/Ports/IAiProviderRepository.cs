using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.AI.Providers.Ports;

/// <summary>
/// ADR-057 (FAI-T011a): acesso ao cadastro de providers de IA e ao
/// roteamento capacidade → provider/modelo. Uma linha por capacidade
/// (índice único).
/// </summary>
public interface IAiProviderRepository : IRepository<AiProvider>
{
    Task<AiProvider?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProvider>> ListAsync(CancellationToken cancellationToken = default);

    Task<AiProviderCapability?> GetCapabilityRouteAsync(
        string capability, CancellationToken cancellationToken = default);

    Task UpsertCapabilityRouteAsync(
        AiProviderCapability route, CancellationToken cancellationToken = default);

    Task<bool> CapabilityRouteExistsAsync(
        string capability, CancellationToken cancellationToken = default);

    /// <summary>Remove a rota da capacidade quando existir (idempotente).</summary>
    Task RemoveCapabilityRouteAsync(
        string capability, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
