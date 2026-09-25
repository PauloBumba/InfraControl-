using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

public interface IRemoteSessionRepository : IRepository<RemoteSession>
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sessões do device, mais recentes primeiro (F11-T001).</summary>
    Task<IReadOnlyList<RemoteSession>> GetRecentByDeviceAsync(
        Guid deviceId, int limit, CancellationToken cancellationToken = default);
}
