using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.Application.RemoteAccess.Credentials.Ports;

public interface IDeviceCredentialRepository : IRepository<DeviceCredential>
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Credencial do device, se cadastrada (F11-T006).</summary>
    Task<DeviceCredential?> GetByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken = default);
}
