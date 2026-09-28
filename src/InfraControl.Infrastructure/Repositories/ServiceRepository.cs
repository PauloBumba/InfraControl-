using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Services.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class ServiceRepository(AppDbContext dbContext) : IServiceRepository
{
    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        await dbContext.Services.AddAsync(service, cancellationToken);
    }

    public Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Services
            .SingleOrDefaultAsync(service => service.Id == id, cancellationToken);

    public void Remove(Service service)
        => dbContext.Services.Remove(service);

    public Task<Service?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Services
            .FirstOrDefaultAsync(
                service => service.Name.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<PagedResult<Service>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Services.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLowerInvariant();
            query = query.Where(service =>
                service.Name.ToLower().Contains(normalized)
                || (service.Description != null && service.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(service => service.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Service>(items, totalCount, page, pageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
