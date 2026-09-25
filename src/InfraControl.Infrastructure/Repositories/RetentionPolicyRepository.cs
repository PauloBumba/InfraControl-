using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class RetentionPolicyRepository(AppDbContext dbContext)
    : IRetentionPolicyRepository
{
    public Task<IReadOnlyList<RetentionPolicy>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => dbContext.RetentionPolicies
            .AsNoTracking()
            .OrderBy(p => p.DataType)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<RetentionPolicy>>(
                task => task.Result, CancellationToken.None);

    public Task<RetentionPolicy?> GetByTypeAsync(
        RetentionDataType dataType, CancellationToken cancellationToken = default)
        => dbContext.RetentionPolicies
            .FirstOrDefaultAsync(p => p.DataType == dataType, cancellationToken);

    public async Task AddAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(policy).State == EntityState.Detached)
            await dbContext.RetentionPolicies.AddAsync(policy, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        RetentionDataType dataType, CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.RetentionPolicies
            .FirstOrDefaultAsync(p => p.DataType == dataType, cancellationToken);

        if (policy is null)
            return false;

        dbContext.RetentionPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
