using InfraControl.Application.Company.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Singleton de configuração institucional (spec §3).</summary>
public sealed class CompanySettingsRepository(AppDbContext dbContext)
    : ICompanySettingsRepository
{
    public Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default) =>
        dbContext.CompanySettings
            .Include(s => s.Departments)
            .OrderBy(s => s.CreateDateTime)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CompanySettings?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.CompanySettings
            .Include(s => s.Departments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(CompanySettings entity, CancellationToken cancellationToken = default) =>
        await dbContext.CompanySettings.AddAsync(entity, cancellationToken);

    public void Remove(CompanySettings entity) =>
        dbContext.CompanySettings.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
