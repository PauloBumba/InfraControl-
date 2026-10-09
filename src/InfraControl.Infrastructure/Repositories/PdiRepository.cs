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
            join manager in dbContext.DomainUsers.AsNoTracking()
                on plan.ManagerId equals manager.Id into managerJoin
            where onlyUserId == null || plan.UserId == onlyUserId
            orderby owner.DisplayName
            select new
            {
                Plan = plan,
                owner.DisplayName,
                owner.Email,
                ManagerName = managerJoin.Select(m => m.DisplayName).FirstOrDefault()
            };

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(row => new PdiWithOwner(
                row.Plan, row.DisplayName, row.Email, row.ManagerName))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PdiWithOwner>> ListByManagerAsync(
        Guid managerId, CancellationToken cancellationToken = default)
    {
        var query =
            from plan in dbContext.IndividualDevelopmentPlans.AsNoTracking()
            join owner in dbContext.DomainUsers.AsNoTracking()
                on plan.UserId equals owner.Id
            where plan.ManagerId == managerId
            orderby owner.DisplayName
            select new
            {
                Plan = plan,
                owner.DisplayName,
                owner.Email
            };

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(row => new PdiWithOwner(
                row.Plan, row.DisplayName, row.Email, ManagerName: null))
            .ToArray();
    }

    public async Task<PlanOwner?> GetOwnerAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => await dbContext.DomainUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new PlanOwner(user.DisplayName, user.Email))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetUserNameAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => await dbContext.DomainUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PdiHistoryWithActor>> ListHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var query =
            from entry in dbContext.PdiHistoryEntries.AsNoTracking()
            where entry.UserId == userId
            orderby entry.CreateDateTime descending, entry.Id descending
            join actor in dbContext.DomainUsers.AsNoTracking()
                on entry.ActorId equals actor.Id into actorJoin
            select new
            {
                Entry = entry,
                ActorName = actorJoin.Select(a => a.DisplayName).FirstOrDefault()
            };

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(row => new PdiHistoryWithActor(row.Entry, row.ActorName))
            .ToArray();
    }

    public void AddHistoryRange(IReadOnlyCollection<PdiHistoryEntry> entries)
        => dbContext.PdiHistoryEntries.AddRange(entries);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken);
}
