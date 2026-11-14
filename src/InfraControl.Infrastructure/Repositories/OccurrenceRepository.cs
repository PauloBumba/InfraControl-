using InfraControl.Application.Notifications.Transitions.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Estado atual + histórico de transições das ocorrências (gate de notificação).</summary>
public sealed class OccurrenceRepository(AppDbContext dbContext)
    : IOccurrenceRepository
{
    public async Task<Occurrence?> GetByKeyAsync(
        string occurrenceKey, CancellationToken cancellationToken = default)
        => await dbContext.Occurrences
            .FirstOrDefaultAsync(o => o.OccurrenceKey == occurrenceKey, cancellationToken);

    public async Task ReloadAsync(
        Occurrence occurrence, CancellationToken cancellationToken = default)
        => await dbContext.Entry(occurrence).ReloadAsync(cancellationToken);

    public void Remove(Occurrence occurrence)
        => dbContext.Occurrences.Remove(occurrence);

    public void RemoveTransition(OccurrenceTransition transition)
        => dbContext.OccurrenceTransitions.Remove(transition);

    public async Task AddAsync(
        Occurrence occurrence, CancellationToken cancellationToken = default)
        => await dbContext.Occurrences.AddAsync(occurrence, cancellationToken);

    public async Task AddTransitionAsync(
        OccurrenceTransition transition, CancellationToken cancellationToken = default)
        => await dbContext.OccurrenceTransitions.AddAsync(transition, cancellationToken);

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
