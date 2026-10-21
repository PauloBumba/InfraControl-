using InfraControl.Domain.Entities;
using InfraControl.Domain.Repositories;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public class AnnouncementRepository : IAnnouncementRepository
{
    private readonly AppDbContext _context;

    public AnnouncementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Announcement?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        return await _context.Announcements
            .FirstOrDefaultAsync(a => a.Fingerprint == fingerprint && !a.IsAcknowledged, cancellationToken);
    }

    public async Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        await _context.Announcements.AddAsync(announcement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Announcement?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Announcements
            .Where(a => !a.IsAcknowledged && a.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Announcement>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Announcements
            .Where(a => !a.IsAcknowledged && a.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);
    }
}
