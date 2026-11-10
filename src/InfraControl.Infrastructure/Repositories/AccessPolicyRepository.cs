using InfraControl.Application.Common.Security.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Grants de acesso dinâmico por role×módulo (ADR-066).</summary>
public sealed class AccessPolicyRepository(AppDbContext dbContext)
    : IAccessPolicyRepository
{
    public async Task<IReadOnlyList<AccessPolicyRule>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.AccessPolicyRules.AsNoTracking().ToListAsync(cancellationToken);

    public Task<AccessPolicyRule?> FindAsync(
        string roleKey, string moduleKey, CancellationToken cancellationToken = default) =>
        dbContext.AccessPolicyRules
            .FirstOrDefaultAsync(
                r => r.RoleKey == roleKey && r.ModuleKey == moduleKey, cancellationToken);

    public Task<AccessPolicyRule?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AccessPolicyRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(
        AccessPolicyRule entity, CancellationToken cancellationToken = default) =>
        await dbContext.AccessPolicyRules.AddAsync(entity, cancellationToken);

    public void Remove(AccessPolicyRule entity) =>
        dbContext.AccessPolicyRules.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
