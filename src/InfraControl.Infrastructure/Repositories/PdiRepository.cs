using InfraControl.Application.Pdi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class PdiRepository(AppDbContext dbContext)
    : IPdiRepository
{
    public Task<IndividualDevelopmentPlan?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.IndividualDevelopmentPlans
            .SingleOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public Task<IndividualDevelopmentPlan?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.IndividualDevelopmentPlans
            .SingleOrDefaultAsync(plan => plan.UserId == userId, cancellationToken);

    public async Task AddAsync(
        IndividualDevelopmentPlan entity, CancellationToken cancellationToken = default)
        => await dbContext.IndividualDevelopmentPlans.AddAsync(entity, cancellationToken);

    public void Remove(IndividualDevelopmentPlan entity)
        => dbContext.IndividualDevelopmentPlans.Remove(entity);

    public async Task<IReadOnlyCollection<PdiWithOwner>> ListWithOwnersAsync(
        Guid? onlyUserId = null, CancellationToken cancellationToken = default)
    {
        var query =
            from plan in dbContext.IndividualDevelopmentPlans.AsNoTracking()
            join owner in dbContext.DomainUsers.AsNoTracking()
                on plan.UserId equals owner.Id
            where onlyUserId == null || plan.UserId == onlyUserId
            orderby owner.DisplayName
            select new { Plan = plan, owner.DisplayName, owner.Email };

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(row => new PdiWithOwner(row.Plan, row.DisplayName, row.Email))
            .ToArray();
    }

    public async Task<PlanOwner?> GetOwnerAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => await dbContext.DomainUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new PlanOwner(user.DisplayName, user.Email))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken);
}
