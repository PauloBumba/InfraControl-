using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Operations.Cluster.Ports;

/// <summary>Persistência das instâncias do cluster (réplicas/nós).</summary>
public interface IServiceInstanceRepository : IRepository<ServiceInstance>
{
    Task<IReadOnlyList<ServiceInstance>> ListAsync(CancellationToken cancellationToken = default);
    Task<ServiceInstance?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ServiceInstance entity, CancellationToken cancellationToken = default);
    void Remove(ServiceInstance entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
