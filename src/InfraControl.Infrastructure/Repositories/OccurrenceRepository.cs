using InfraControl.Application.Notifications.Transitions.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        catch (DbUpdateException exception) when (IsOccurrenceKeyViolation(exception))
        {
            // Corrida de INSERT na chave única (dois consumers criam a
            // mesma ocorrência): equivalente ao conflito de concorrência —
            // o gate desfaz os pendentes, relê o estado persistido e
            // reavalia. Sem isto o 23505 subia como DbUpdateException,
            // o consumer ia para a fila de erro e a notificação se perdia.
            return false;
        }
    }

    private static bool IsOccurrenceKeyViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_occurrences_key"
        };
}
