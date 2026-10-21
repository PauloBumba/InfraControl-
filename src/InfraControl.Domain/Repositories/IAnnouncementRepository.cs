using InfraControl.Domain.Entities;

namespace InfraControl.Domain.Repositories;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default);
    Task<Announcement?> GetNextPendingAsync(CancellationToken cancellationToken = default);
    Task<List<Announcement>> GetPendingAsync(CancellationToken cancellationToken = default);
}
