using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class DeviceCredentialRepository(AppDbContext dbContext)
    : IDeviceCredentialRepository
{
    public async Task<DeviceCredential?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.DeviceCredentials
            .SingleOrDefaultAsync(credential => credential.Id == id, cancellationToken);

    public async Task<DeviceCredential?> GetByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
        => await dbContext.DeviceCredentials
            .SingleOrDefaultAsync(
                credential => credential.DeviceId == deviceId, cancellationToken);

    public async Task AddAsync(
        DeviceCredential entity, CancellationToken cancellationToken = default)
        => await dbContext.DeviceCredentials.AddAsync(entity, cancellationToken);

    public void Remove(DeviceCredential entity)
        => dbContext.DeviceCredentials.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
