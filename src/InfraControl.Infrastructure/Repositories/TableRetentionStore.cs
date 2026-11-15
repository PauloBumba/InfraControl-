using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Store EF das políticas de retenção por tabela.</summary>
public sealed class TableRetentionStore(AppDbContext dbContext)
    : ITableRetentionStore
{
    public async Task<IReadOnlyList<TableRetentionPolicy>> ListAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.TableRetentionPolicies
            .AsNoTracking()
            .OrderBy(p => p.EntityName)
            .ToListAsync(cancellationToken);

    public async Task<TableRetentionPolicy?> GetAsync(
        string entityName, CancellationToken cancellationToken = default)
        => await dbContext.TableRetentionPolicies
            .FirstOrDefaultAsync(
                p => p.EntityName == entityName, cancellationToken);

    public async Task UpsertAsync(
        TableRetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TableRetentionPolicies
            .FirstOrDefaultAsync(p => p.EntityName == policy.EntityName, cancellationToken);
        if (existing is null)
            await dbContext.TableRetentionPolicies.AddAsync(policy, cancellationToken);
        else
            dbContext.Entry(existing).CurrentValues.SetValues(policy);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string entityName, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TableRetentionPolicies
            .FirstOrDefaultAsync(
                p => p.EntityName == entityName, cancellationToken);
        if (existing is null)
            return false;

        dbContext.TableRetentionPolicies.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
