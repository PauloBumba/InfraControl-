using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Network.Connections.Ports;

public interface IConnectionRepository : IRepository<Connection>
{
    /// <summary>Busca o par DIRIGIDO (origem, destino) — unicidade natural.</summary>
    Task<Connection?> GetPairAsync(
        Guid sourceInterfaceId,
        Guid targetInterfaceId,
        CancellationToken cancellationToken = default);

    /// <summary>Conexão com as pontas resolvidas (join) ou null (→ 404).</summary>
    Task<ConnectionWithEndpoints?> GetWithEndpointsAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteNetworkInterface para bloquear exclusão (→ 409).</summary>
    Task<bool> HasConnectionsForInterfaceAsync(
        Guid interfaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista paginada com as duas pontas resolvidas (join com
    /// NetworkInterface) — filtro por device em qualquer lado e termo nos
    /// nomes das interfaces; ordena por origem/destino.
    /// </summary>
    Task<PagedResult<ConnectionWithEndpoints>> SearchAsync(
        Guid? deviceId,
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Conexão com as pontas já carregadas (projeção do search).</summary>
public sealed record ConnectionWithEndpoints(
    Connection Connection,
    NetworkInterface Source,
    NetworkInterface Target);
