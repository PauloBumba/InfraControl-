using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class RemoteSessionRepository(AppDbContext dbContext)
    : IRemoteSessionRepository
{
    public async Task<RemoteSession?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.RemoteSessions
            .SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task AddAsync(
        RemoteSession entity, CancellationToken cancellationToken = default)
        => await dbContext.RemoteSessions.AddAsync(entity, cancellationToken);

    public void Remove(RemoteSession entity)
        => dbContext.RemoteSessions.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<RemoteSession>> GetRecentByDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default)
        => await dbContext.RemoteSessions
            .AsNoTracking()
            .Where(session => session.DeviceId == deviceId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
