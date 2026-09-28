using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Services.Ports;

public interface IServiceRepository : IRepository<Service>
{
    /// <summary>Busca por nome sem distinção de maiúsculas/minúsculas.</summary>
    Task<Service?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<Service>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
