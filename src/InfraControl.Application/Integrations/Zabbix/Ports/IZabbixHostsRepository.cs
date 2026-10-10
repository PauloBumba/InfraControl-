using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Integrations.Zabbix.Ports;

/// <summary>
/// F27 Fase 3.1: persistência dos hosts do Zabbix sincronizados
/// (read-model com busca/paginação para a página Hosts monitorados).
/// </summary>
public interface IZabbixHostsRepository
{
    Task<ZabbixHost?> GetByHostIdAsync(
        string hostId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ZabbixHost>> ListAsync(CancellationToken cancellationToken);

    Task<PagedResult<ZabbixHost>> SearchAsync(
        string? term,
        bool? enabled,
        bool? missing,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task AddAsync(ZabbixHost host, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
