using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Registro administrável de instâncias/réplicas do cluster.</summary>
public sealed class ServiceInstanceRepository(AppDbContext dbContext)
    : IServiceInstanceRepository
{
    public async Task<IReadOnlyList<ServiceInstance>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.ServiceInstances.AsNoTracking()
            .OrderBy(i => i.Kind).ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public Task<ServiceInstance?> FindAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ServiceInstances.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<ServiceInstance?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ServiceInstances.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task AddAsync(
        ServiceInstance entity, CancellationToken cancellationToken = default) =>
        await dbContext.ServiceInstances.AddAsync(entity, cancellationToken);

    public void Remove(ServiceInstance entity) =>
        dbContext.ServiceInstances.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
