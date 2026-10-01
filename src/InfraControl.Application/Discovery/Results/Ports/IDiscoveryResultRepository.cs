using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Discovery.Results.Ports;

public interface IDiscoveryResultRepository : IRepository<DiscoveryResult>
{
    Task<PagedResult<DiscoveryResult>> SearchByRunAsync(
        Guid discoveryRunId,
        DiscoveryResultKind? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F18-T008: rows dos kinds pedidos em TODAS as execuções — a agregação
    /// de clientes (dedupe por MAC) acontece no handler, porque Values é JSON
    /// serializado numa coluna de texto e não há dedupe por MAC portável
    /// entre Sqlite (unit) e Postgres (integração).
    /// </summary>
    Task<IReadOnlyList<DiscoveryResult>> ListByKindsAsync(
        IReadOnlyCollection<DiscoveryResultKind> kinds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
